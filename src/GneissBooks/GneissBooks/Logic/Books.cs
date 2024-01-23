using GneissBooks.Saft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace GneissBooks;

/// <summary>
/// Basic accounting system. Very tailored, probably not suitable for most businesses out of the box.
/// </summary>
internal class Books
{
    /// <summary>
    /// Global accessor for convenience
    /// </summary>
    public static Books? Currrent => _current;

    Saft.AuditFile? books;

    public List<AuditFileMasterFilesCustomer> Customers { get; private set; } = new();
    public List<AuditFileMasterFilesSupplier> Suppliers { get; private set; } = new();
    public List<AuditFileGeneralLedgerEntriesJournalTransaction> Transactions { get; private set; } = new();
    // Todo store and make accessible accounts and tax information

    int nextSourceDocumentId = 1;
    int nextTransactionId = 1;
    int nextCustomerId = 1;
    int nextSupplierId = 1;

    const decimal highTaxRate = 25m;

    static Books? _current;

    public Books()
    {
        GenerateDefaultEmpty();
        _current = this;
    }

    /// <summary>
    /// Adds a transaction to the ledger.
    /// </summary>
    /// <param name="date">Time the transaction took place.</param>
    /// <param name="description">Text description of the transaction.</param>
    /// <param name="lines">Individual amounts on the ledger, for one account each.</param>
    /// <returns>Source document ID. The file should be renamed to, or otherwise stored with, this new reference.</returns>
    public async Task<string> AddTransaction(DateTime date, string description, IEnumerable<TransactionLine> lines)
    {
        var formattedLines = new List<AuditFileGeneralLedgerEntriesJournalTransactionLine>();
        int recordId = 1;
        var sourceDocumentId = (nextSourceDocumentId++).ToString();
        foreach (var line in lines)
        {
            var formattedLine = new AuditFileGeneralLedgerEntriesJournalTransactionLine
            {
                RecordID = (recordId++).ToString(),
                AccountID = line.AccountId,
                Description = line.Description,
                Item = new AmountStructure(),
                ItemElementName = line.Amount >= 0 ? ItemChoiceType4.CreditAmount : ItemChoiceType4.DebitAmount,
                SourceDocumentID = sourceDocumentId,
            };
            if (line.SupplierId != null)
                formattedLine.SupplierID = line.SupplierId;
            if (line.CustomerId != null)
                formattedLine.CustomerID = line.CustomerId;

            if (!string.IsNullOrEmpty(line.Currency))
            {
                // Convert currency
                formattedLine.Item.CurrencyAmount = Math.Abs(line.Amount);
                var exchangeRate = await ExchangeRateApi.GetExchangeRateInNok(line.Currency, DateOnly.FromDateTime(date));
                formattedLine.Item.Amount = formattedLine.Item.CurrencyAmount * exchangeRate;
                formattedLine.Item.ExchangeRate = exchangeRate;
                formattedLine.Item.ExchangeRateSpecified = true;
                formattedLine.Item.CurrencyCode = line.Currency;
            }
            else
            {
                formattedLine.Item.Amount = Math.Abs(line.Amount);
                formattedLine.Item.ExchangeRateSpecified = false;
            }

            if (!string.IsNullOrEmpty(line.TaxCode))
            {
                var taxInfo = new TaxInformationStructure() { TaxCode = line.TaxCode, TaxPercentageSpecified = true };
                if (line.TaxCode == TaxCodes.NoTax || line.TaxCode == TaxCodes.Export)
                {
                    taxInfo.TaxPercentage = 0m;
                    taxInfo.TaxAmount = new AmountStructure() { Amount = 0m };
                }
                else // only high rate implemented
                {
                    taxInfo.TaxPercentage = highTaxRate;
                    taxInfo.TaxAmount = new AmountStructure() { Amount = formattedLine.Item.Amount * highTaxRate / 100m };
                }

                formattedLine.TaxInformation = new TaxInformationStructure[] { taxInfo };
            }

            formattedLines.Add(formattedLine);
        }

        var index = 0;
        while (index < Transactions.Count) 
        {
            if (Transactions[index].TransactionDate > date)
                break;

            index++;
        }

        Transactions.Insert(index, new AuditFileGeneralLedgerEntriesJournalTransaction()
        {
            TransactionID = (nextTransactionId++).ToString(),
            Period = date.Month.ToString(),
            PeriodYear = date.Year.ToString(),
            TransactionDate = date,
            Description = description,
            SystemEntryDate = DateTime.Now,
            GLPostingDate = date,
            Line = formattedLines.ToArray()
        });

        return sourceDocumentId;
    }

    /// <summary>
    /// Manually adds a raw transaction to the ledger, for advanced use. NB: Normally you should use AddTransaction(DateTime, string, IEnumerable<TransactionLine>) instead.
    /// </summary>
    /// <param name="transaction"></param>
    /*public void AddTransaction(AuditFileGeneralLedgerEntriesJournalTransaction transaction)
    {
        Transactions.Add(transaction);
    }*/

    /// <summary>
    /// Gets the underlying SAF-T data, for manual modification. Be careful not to break stuff.
    /// </summary>
    /// <returns>SAF-T format books</returns>
    public AuditFile? GetRawBooks()
    {
        return books;
    }

    /*public IEnumerable<AuditFileMasterFilesCustomer> FindPotentialMatchingCustomers(string? firstName, string? lastName, string? company, string? postCode)
    {
        var similarity = 0;
        foreach (var customer in customers)
        {
            if (postCode != null && customer.Address.First().PostalCode == postCode)
                similarity += 2;
            if (firstName != null && customer.)
                similarity += 2;
        }
    }*/

    public void Load(string filename)
    {
        books = Saft.SaftHelper.Deserialize(filename);
        if (books == null)
            throw new Exception("Could not load SAF-T file properly, result was empty.");

        Transactions = books.GeneralLedgerEntries.Journal.First().Transaction.ToList();
        Customers = books.MasterFiles.Customers.ToList();
        Suppliers = books.MasterFiles.Suppliers.ToList();

        nextSourceDocumentId = 1;
        nextTransactionId = 1;
        foreach (var transaction in Transactions)
        {
            if (int.TryParse(transaction.TransactionID, out int transactionId) && transactionId >= nextTransactionId)
                nextTransactionId = transactionId + 1;
            if (int.TryParse(transaction.Line.First().SourceDocumentID, out int sourceDocumentId) && sourceDocumentId >= nextSourceDocumentId)
                nextSourceDocumentId = sourceDocumentId + 1;
        }
        nextCustomerId = 1;
        foreach (var customer in Customers)
        {
            if (int.TryParse(customer.CustomerID, out int customerId) && customerId >= nextCustomerId)
                nextCustomerId = customerId + 1;
        }
        nextSupplierId = 1;
        foreach (var supplier in Suppliers)
        {
            if (int.TryParse(supplier.SupplierID, out int supplierId) && supplierId >= nextSupplierId)
                nextSupplierId = supplierId + 1;
        }
    }

    public void Save(string filename)
    {
        if (books == null)
            throw new Exception("Books must be initialized with Load() or GenerateDefaultEmpty() first");

        books.MasterFiles.Customers = Customers.ToArray();
        books.MasterFiles.Suppliers = Suppliers.ToArray();
        books.GeneralLedgerEntries.Journal.First().Transaction = Transactions.ToArray();

        decimal totalDebit = 0;
        decimal totalCredit = 0;
        foreach (var transaction in Transactions)
        {
            foreach (var line in transaction.Line)
            {
                if (line.ItemElementName == ItemChoiceType4.CreditAmount)
                    totalCredit += line.Item.Amount;
                else
                    totalDebit += line.Item.Amount;
            }
        }
        books.GeneralLedgerEntries.TotalDebit = totalDebit;
        books.GeneralLedgerEntries.TotalCredit = totalCredit;
        // todo closing credit/debit of accounts

        Saft.SaftHelper.Serialize(books, filename);
    }


    private void GenerateDefaultEmpty()
    {
        books = new();

        books.Header = new();
        books.Header.AuditFileDateCreated = DateTime.Now;
        books.Header.Company = new()
        {
            Name = "Mikkelsen Innovasjon",
            RegistrationNumber = "923589155",
            TaxRegistration = new Saft.TaxIDStructure[] {
                new()
                {
                    TaxNumber = "923589155MVA",
                    TaxAuthority = Saft.TaxIDStructureTaxAuthority.Skatteetaten
                }
            },
            Address = new Saft.AddressStructure[] {
                new()
                {
                    StreetName = "Hamrehaugen",
                    Number = "62",
                    PostalCode = "5161",
                    City = "Laksevåg",
                    Region = "Vestland",
                    Country = "NO",
                }
            },
            Contact = new Saft.ContactInformationStructure[] {
                new()
                {
                    ContactPerson = new()
                    {
                        BirthName = "Gitle",
                        LastName = "Mikkelsen",
                    },
                    Telephone = "47639451",
                    Email = "gitlem@gmail.com",
                }
            }
        };
        books.Header.DefaultCurrencyCode = "NOK";

        books.MasterFiles = new();
        AddDefaultAccounts();
        books.MasterFiles.TaxTable = new AuditFileMasterFilesTaxTableEntry[]
        {
            new()
            {
                TaxType = AuditFileMasterFilesTaxTableEntryTaxType.MVA,
                Description = AuditFileMasterFilesTaxTableEntryDescription.Merverdiavgift,
                TaxCodeDetails = new AuditFileMasterFilesTaxTableEntryTaxCodeDetails[]
                {
                    new()
                    {
                        TaxCode = "0",
                        Description = "Ingen avgifter",
                        Item = 0m,
                        StandardTaxCode = "0",
                        Country = "NO",
                        BaseRate = new[] { 100m }
                    }
                }
            },
            new()
            {
                TaxType = AuditFileMasterFilesTaxTableEntryTaxType.MVA,
                Description = AuditFileMasterFilesTaxTableEntryDescription.Merverdiavgift,
                TaxCodeDetails = new AuditFileMasterFilesTaxTableEntryTaxCodeDetails[]
                {
                    new()
                    {
                        TaxCode = "1",
                        Description = "Inngående avgift, høy sats",
                        Item = highTaxRate,
                        StandardTaxCode = "1",
                        Country = "NO",
                        BaseRate = new[] { 100m }
                    }
                }
            },
            new()
            {
                TaxType = AuditFileMasterFilesTaxTableEntryTaxType.MVA,
                Description = AuditFileMasterFilesTaxTableEntryDescription.Merverdiavgift,
                TaxCodeDetails = new AuditFileMasterFilesTaxTableEntryTaxCodeDetails[]
                {
                    new()
                    {
                        TaxCode = "3",
                        Description = "Utgående avgift, høy sats",
                        Item = highTaxRate,
                        StandardTaxCode = "3",
                        Country = "NO",
                        BaseRate = new[] { 100m }
                    }
                }
            },
            new()
            {
                TaxType = AuditFileMasterFilesTaxTableEntryTaxType.MVA,
                Description = AuditFileMasterFilesTaxTableEntryDescription.Merverdiavgift,
                TaxCodeDetails = new AuditFileMasterFilesTaxTableEntryTaxCodeDetails[]
                {
                    new()
                    {
                        TaxCode = "21",
                        Description = "Innførsel av varer, høy sats. Leverandørfaktura.",
                        Item = highTaxRate,
                        StandardTaxCode = "21",
                        Country = "NO",
                        BaseRate = new[] { 100m }
                    }
                }
            },
            new()
            {
                TaxType = AuditFileMasterFilesTaxTableEntryTaxType.MVA,
                Description = AuditFileMasterFilesTaxTableEntryDescription.Merverdiavgift,
                TaxCodeDetails = new AuditFileMasterFilesTaxTableEntryTaxCodeDetails[]
                {
                    new()
                    {
                        TaxCode = "81",
                        Description = "Innførsel av varer, høy sats. Grunnlag til MVA-melding.",
                        Item = highTaxRate,
                        StandardTaxCode = "81",
                        Country = "NO",
                        BaseRate = new[] { 100m }
                    }
                }
            },
            new()
            {
                TaxType = AuditFileMasterFilesTaxTableEntryTaxType.MVA,
                Description = AuditFileMasterFilesTaxTableEntryDescription.Merverdiavgift,
                TaxCodeDetails = new AuditFileMasterFilesTaxTableEntryTaxCodeDetails[]
                {
                    new()
                    {
                        TaxCode = "52",
                        Description = "Utførsel av varer og tjenester, 0%",
                        Item = 0m,
                        StandardTaxCode = "52",
                        Country = "NO",
                        BaseRate = new[] { 100m }
                    }
                }
            },
        };

        books.GeneralLedgerEntries = new();
        var journal = new AuditFileGeneralLedgerEntriesJournal
        {
            Description = "Bok",
            JournalID = "GL",
            Type = "GL",
        };
        books.GeneralLedgerEntries.Journal = new Saft.AuditFileGeneralLedgerEntriesJournal[1] { journal };

    }

    private void AddDefaultAccounts()
    {
        if (books == null)
            throw new Exception("Books must be initialized with Load() or GenerateDefaultEmpty() first");

        var accounts = new List<AuditFileMasterFilesAccount>();
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "1420",
            StandardAccountID = "14",
            AccountDescription = "Lager varer/deler",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 174_606.23m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "1501",
            StandardAccountID = "15",
            AccountDescription = "Kunder, Paypal-betaling",
            ItemElementName = ItemChoiceType.OpeningDebitBalance,
            Item = 2109.12m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "1502",
            StandardAccountID = "15",
            AccountDescription = "Kunder, Stripe-betaling",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 27808.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "1503",
            StandardAccountID = "15",
            AccountDescription = "Kunder, Ebay-betaling",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "1504",
            StandardAccountID = "15",
            AccountDescription = "Kunder, via Amazon",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "1505",
            StandardAccountID = "15",
            AccountDescription = "Kunder, andre",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "2060",
            StandardAccountID = "20",
            AccountDescription = "Privat uttak/utlegg",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "2400",
            StandardAccountID = "24",
            AccountDescription = "Leverandører",
            ItemElementName = ItemChoiceType.OpeningDebitBalance,
            Item = 304.65m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "2700",
            StandardAccountID = "27",
            AccountDescription = "Utgående MVA",
            ItemElementName = ItemChoiceType.OpeningDebitBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "2710",
            StandardAccountID = "27",
            AccountDescription = "Inngående MVA",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "2740",
            StandardAccountID = "27",
            AccountDescription = "Oppgjør MVA",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "3000",
            StandardAccountID = "30",
            AccountDescription = "Salg, Høy MVA-sats",
            ItemElementName = ItemChoiceType.OpeningDebitBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "3100",
            StandardAccountID = "31",
            AccountDescription = "Salg, MVA-fritatt",
            ItemElementName = ItemChoiceType.OpeningDebitBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "3705",
            StandardAccountID = "37",
            AccountDescription = "Provisjon, MVA-fritatt",
            ItemElementName = ItemChoiceType.OpeningDebitBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "3900",
            StandardAccountID = "39",
            AccountDescription = "Andre inntekter",
            ItemElementName = ItemChoiceType.OpeningDebitBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "4100",
            StandardAccountID = "41",
            AccountDescription = "Forbruk varer/deler",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "4160",
            StandardAccountID = "41",
            AccountDescription = "Frakt/toll innkjøp varer/deler",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "6100",
            StandardAccountID = "61",
            AccountDescription = "Fraktkostnader",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "6100",
            StandardAccountID = "61",
            AccountDescription = "Toll- og spedisjonskostnader",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "6540",
            StandardAccountID = "65",
            AccountDescription = "Forbruk inventar",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "6552",
            StandardAccountID = "65",
            AccountDescription = "Forbruk programvare",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "6560",
            StandardAccountID = "65",
            AccountDescription = "Forbruk rekvisita",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "6590",
            StandardAccountID = "65",
            AccountDescription = "Forbruk annet",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "6901",
            StandardAccountID = "69",
            AccountDescription = "Telefonkostnader",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "6907",
            StandardAccountID = "69",
            AccountDescription = "Internettkostnader",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "7300",
            StandardAccountID = "73",
            AccountDescription = "Salgskostnader",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "7320",
            StandardAccountID = "73",
            AccountDescription = "Markedsføringskostnader",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "7550",
            StandardAccountID = "75",
            AccountDescription = "Garanti- og returkostnad",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "7770",
            StandardAccountID = "77",
            AccountDescription = "Betalingsgebyr",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "7798",
            StandardAccountID = "77",
            AccountDescription = "Andre kostnader",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "8060",
            StandardAccountID = "80",
            AccountDescription = "Valutagevinst",
            ItemElementName = ItemChoiceType.OpeningDebitBalance,
            Item = 0.00m,
        });
        accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "8160",
            StandardAccountID = "81",
            AccountDescription = "Valutatap",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        books.MasterFiles.GeneralLedgerAccounts = accounts.ToArray();
    }
}

public class TransactionLine
{
    public decimal Amount { get; set; }
    public string AccountId { get; set; }
    public string? SupplierId { get; set; }
    public string? CustomerId { get; set; }
    public string Description { get; set; }
    public string? Currency { get; set; }
    public string? TaxCode { get; set; }

    /// <summary>
    /// </summary>
    /// <param name="amount">Amount in the given currency. NOK if currency not specified.</param>
    /// <param name="accountId">Account ID from books' General Ledger. Must exist in the books' account specification.</param>
    /// <param name="description">Text description of line.</param>
    /// <param name="currency">Three-letter currency code for amount. Default null = NOK</param>
    /// <param name="customerId">Customer ID from books' customer specification. Must exist there.</param>
    /// <param name="supplierId">Supplier ID from books' supplier specification. Must exist there.</param>
    /// <exception cref="Exception"></exception>
    public TransactionLine(decimal amount, string accountId, string description = "", string? currency = null, string? taxCode = null, string? customerId = null, string? supplierId = null)
    {
        Amount = amount;
        AccountId = accountId;
        Description = description;
        Currency = string.IsNullOrWhiteSpace(currency) ? null : currency;
        CustomerId = string.IsNullOrWhiteSpace(customerId) ? null : customerId;
        SupplierId = string.IsNullOrWhiteSpace(supplierId) ? null : supplierId;
        TaxCode = string.IsNullOrWhiteSpace(taxCode) ? null : taxCode;

        if (CustomerId != null && SupplierId != null)
            throw new Exception("Cannot specify both customer ID and supplier ID in a transaction line");
    }
}

public class TaxCodes
{
    public const string NoTax = "0";
    public const string IncomingPurchaseTaxHighRate = "1";
    public const string OutgoingSaleTaxHighRate = "3";
    public const string ImportHighRate_SupplierInvoice = "21";
    public const string ImportHighRate_Tax = "81";
    public const string Export = "52";
}