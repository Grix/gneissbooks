using GneissBooks.Saft;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace GneissBooks;

/// <summary>
/// Basic accounting system. Very tailored, probably not suitable for most businesses out of the box.
/// </summary>
internal class SaftBooks
{
    /// <summary>
    /// Global accessor for convenience
    /// </summary>
    public static SaftBooks? Currrent => _current;

    public List<AuditFileMasterFilesCustomer> Customers { get; private set; } = new();
    public List<AuditFileMasterFilesSupplier> Suppliers { get; private set; } = new();
    public List<AuditFileGeneralLedgerEntriesJournalTransaction> Transactions { get; private set; } = new();
    public List<AuditFileMasterFilesAccount> Accounts { get; private set; } = new();
    public List<AuditFileMasterFilesTaxTableEntry> TaxClasses { get; private set; } = new();

    int nextSourceDocumentId = 1;
    int nextTransactionId = 1;
    int nextCustomerId = 1;
    int nextSupplierId = 1;
    int nextAccountId = 1;

    const decimal highTaxRate = 25m;

    Saft.AuditFile books;

    static SaftBooks? _current;

    public SaftBooks()
    {
        books = new();
        GenerateDefaultEmpty();
        _current = this;
    }

    public async Task<EntityId> FindOrAddCustomer(string? companyName, string? firstName, string? lastName, string? postCode, string? city, string? streetName, 
                                                    string? streetNumber, string? addressLine2, string? countryCode, string? telephone, string? email, decimal startingBalance = 0m, string? customerId = null)
    {
        if (countryCode != null && countryCode.Length != 2)
            throw new Exception("Invalid country code");
        if (companyName == null && lastName == null)
            throw new Exception("Neither person nor company name specified");

        // If existing customerId is specified, modify it
        if (!string.IsNullOrWhiteSpace(customerId))
        {
            var customer = Customers.FirstOrDefault(_customer => { return _customer.CustomerID == customerId; });
            if (customer != null)
            {
                customer.Name = companyName ?? $"{lastName}, {firstName}";
                customer.Contact = (lastName != null) ? new ContactInformationStructure[] {
                    new()
                    {
                        Telephone = telephone,
                        Email = email,
                        ContactPerson = new()
                        {
                            FirstName = firstName,
                            LastName = lastName,
                        }
                    }
                } : null;
                customer.Address = new AddressStructure[] {
                    new()
                    {
                        StreetName = streetName,
                        Number = streetNumber,
                        City = city,
                        PostalCode = postCode,
                        Country = countryCode,
                        AdditionalAddressDetail = addressLine2
                    }
                };
                customer.Item = Math.Abs(startingBalance);
                customer.ItemElementName = startingBalance >= 0 ? ItemChoiceType1.OpeningDebitBalance : ItemChoiceType1.OpeningCreditBalance;

                return new EntityId(customer.CustomerID, customer.AccountID);
            }
        }
        else
        {
            // Search for similar existing and simply return the ID if found, not modifying it
            foreach (var customer in Customers)
            {
                var similarity = 0.0;

                if (telephone == null)
                    similarity += 0.5;
                else if (customer.Contact?.FirstOrDefault()?.Telephone == telephone)
                    similarity += 2;
                if (email != null && customer.Contact?.FirstOrDefault()?.Email == email)
                    similarity += 2;
                if (companyName != null && customer.Name.ToLower() == companyName.ToLower())
                    similarity += 2;

                if (similarity >= 3)
                    return new EntityId(customer.CustomerID, customer.AccountID);

                if (postCode == null)
                    similarity += 0.5;
                else if (customer.Address?.FirstOrDefault()?.PostalCode == postCode)
                    similarity += 1;
                if (firstName != null && customer.Contact?.FirstOrDefault()?.ContactPerson?.FirstName.ToLower() == firstName.ToLower())
                    similarity += 1;
                if (lastName != null && customer.Contact?.FirstOrDefault()?.ContactPerson?.LastName.ToLower() == lastName.ToLower())
                    similarity += 1;

                if (similarity >= 3)
                    return new EntityId(customer.CustomerID, customer.AccountID);
            }
        }

        // No existing found, create new
        var newCustomer = new AuditFileMasterFilesCustomer()
        {
            CustomerID = string.IsNullOrWhiteSpace(customerId) ? (nextCustomerId++.ToString()) : customerId,
            AccountID = nextAccountId++.ToString(),
            Name = companyName ?? $"{lastName}, {firstName}",
            Contact = (lastName != null) ? new ContactInformationStructure[] {
                new()
                {
                    Telephone = telephone,
                    Email = email,
                    ContactPerson = new()
                    {
                        FirstName = firstName,
                        LastName = lastName,
                    }
                }
            } : null,
            Address = new AddressStructure[] {
                new()
                {
                    StreetName = streetName,
                    Number = streetNumber,
                    City = city,
                    PostalCode = postCode,
                    Country = countryCode,
                    AdditionalAddressDetail = addressLine2
                }
            },
            Item = Math.Abs(startingBalance),
            ItemElementName = startingBalance >= 0 ? ItemChoiceType1.OpeningDebitBalance : ItemChoiceType1.OpeningCreditBalance,
            Item1 = 0m,
            Item1ElementName = Item1ChoiceType1.ClosingDebitBalance
        };

        Customers.Add(newCustomer);

        return new EntityId(newCustomer.CustomerID, newCustomer.AccountID);
    }

    public async Task<EntityId> FindOrAddSupplier(string? companyName, string? firstName, string? lastName, string? postCode, string? city, string? streetName,
                                                    string? streetNumber, string? addressLine2, string? countryCode, string? telephone, string? email, decimal startingBalance = 0m, string? supplierId = null)
    {
        if (countryCode != null && countryCode.Length != 2)
            throw new Exception("Invalid country code");
        if (companyName == null && lastName == null)
            throw new Exception("Neither person nor company name specified");

        // If existing supplierId is specified, modify it
        if (!string.IsNullOrWhiteSpace(supplierId))
        {
            var supplier = Suppliers.FirstOrDefault(_supplier => { return _supplier.SupplierID == supplierId; });
            if (supplier != null)
            {
                supplier.Name = companyName ?? $"{lastName}, {firstName}";
                supplier.Contact = (lastName != null) ? new ContactInformationStructure[] {
                    new()
                    {
                        Telephone = telephone,
                        Email = email,
                        ContactPerson = new()
                        {
                            FirstName = firstName,
                            LastName = lastName,
                        }
                    }
                } : null;
                supplier.Address = new AddressStructure[] {
                    new()
                    {
                        StreetName = streetName,
                        Number = streetNumber,
                        City = city,
                        PostalCode = postCode,
                        Country = countryCode,
                        AdditionalAddressDetail = addressLine2
                    }
                };
                supplier.Item = Math.Abs(startingBalance);
                supplier.ItemElementName = startingBalance >= 0 ? ItemChoiceType2.OpeningDebitBalance : ItemChoiceType2.OpeningCreditBalance;

                return new EntityId(supplier.SupplierID, supplier.AccountID);
            }
        }
        else
        {
            // Search for similar existing and simply return the ID if found, not modifying it
            foreach (var supplier in Suppliers)
            {
                var similarity = 0.0;

                if (telephone == null)
                    similarity += 0.5;
                else if (supplier.Contact?.FirstOrDefault()?.Telephone == telephone)
                    similarity += 2;
                if (email != null && supplier.Contact?.FirstOrDefault()?.Email == email)
                    similarity += 2;
                if (companyName != null && supplier.Name == companyName)
                    similarity += 2;

                if (similarity >= 2)
                    return new EntityId(supplier.SupplierID, supplier.AccountID);

                if (postCode == null)
                    similarity += 0.5;
                else if (supplier.Address?.FirstOrDefault()?.PostalCode == postCode)
                    similarity += 1;
                if (firstName != null && supplier.Contact?.FirstOrDefault()?.ContactPerson?.FirstName == firstName)
                    similarity += 1;
                if (lastName != null && supplier.Contact?.FirstOrDefault()?.ContactPerson?.LastName == lastName)
                    similarity += 1;

                if (similarity >= 2)
                    return new EntityId(supplier.SupplierID, supplier.AccountID);
            }
        }

        // No existing found, create new
        var newSupplier = new AuditFileMasterFilesSupplier()
        {
            SupplierID = string.IsNullOrWhiteSpace(supplierId) ? (nextSupplierId++.ToString()) : supplierId,
            AccountID = nextAccountId++.ToString(),
            Name = companyName ?? $"{lastName}, {firstName}",
            Contact = (lastName != null) ? new ContactInformationStructure[] {
                new()
                {
                    Telephone = telephone,
                    Email = email,
                    ContactPerson = new()
                    {
                        FirstName = firstName,
                        LastName = lastName,
                    }
                }
            } : null,
            Address = new AddressStructure[] {
                new()
                {
                    StreetName = streetName,
                    Number = streetNumber,
                    City = city,
                    PostalCode = postCode,
                    Country = countryCode,
                    AdditionalAddressDetail = addressLine2
                }
            },
            Item = Math.Abs(startingBalance),
            ItemElementName = startingBalance >= 0 ? ItemChoiceType2.OpeningDebitBalance : ItemChoiceType2.OpeningCreditBalance,
            Item1 = 0m,
            Item1ElementName = Item1ChoiceType2.ClosingDebitBalance
        };

        Suppliers.Add(newSupplier);

        return new EntityId(newSupplier.SupplierID, newSupplier.AccountID);
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
                ItemElementName = line.Amount >= 0 ? ItemChoiceType4.DebitAmount : ItemChoiceType4.CreditAmount,
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
                var taxClass = TaxClasses.Find(_taxClass => { return _taxClass.TaxCodeDetails?.FirstOrDefault()?.TaxCode == line.TaxCode; });
                if (taxClass != null)
                {
                    var taxRate = taxClass.TaxCodeDetails.First().Item;

                    if (taxRate is AmountStructure)
                        throw new Exception("Flat tax rates are not yet supported");

                    var taxInfo = new TaxInformationStructure
                    {
                        TaxCode = line.TaxCode,
                        TaxPercentageSpecified = true,
                        TaxPercentage = (taxRate is decimal taxPercentage) ? taxPercentage : 0m, // TODO support flat rate tax amount
                        TaxBaseSpecified = line.TaxBase != null,
                    };
                    if (taxInfo.TaxBaseSpecified)
                        taxInfo.TaxBase = (decimal)line.TaxBase!;

                    taxInfo.TaxAmount = new AmountStructure() { Amount = (line.TaxBase ?? Math.Abs(formattedLine.Item.Amount)) * taxInfo.TaxPercentage / 100m };
                    formattedLine.TaxInformation = new TaxInformationStructure[] { taxInfo };
                }
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

        var transaction = new AuditFileGeneralLedgerEntriesJournalTransaction()
        {
            TransactionID = (nextTransactionId++).ToString(),
            Period = date.Month.ToString(),
            PeriodYear = date.Year.ToString(),
            TransactionDate = date,
            Description = description,
            SystemEntryDate = DateTime.Now,
            GLPostingDate = date,
            Line = formattedLines.ToArray(),
        };

        Transactions.Insert(index, transaction);

        UpdateBalancesFromTransaction(transaction);

        return sourceDocumentId;
    }

    /// <summary>
    /// Adds the amounts from a transaction to the total credit and debit balances in the books, such as account, customer and supplier closing balances.
    /// </summary>
    /// <param name="transaction"></param>
    private void UpdateBalancesFromTransaction(AuditFileGeneralLedgerEntriesJournalTransaction transaction)
    {
        foreach (var line in transaction.Line)
        {
            AuditFileMasterFilesAccount? account = null;
            if (!string.IsNullOrEmpty(line.AccountID))
            {
                account = Accounts.FirstOrDefault(_account => { return _account.AccountID == line.AccountID; });
                if (account == null)
                    Debug.WriteLine("WARNING: Undefined account found in transaction: " + line.AccountID);
            }
            AuditFileMasterFilesCustomer? customer = null;
            if (!string.IsNullOrEmpty(line.CustomerID))
            {
                customer = Customers.FirstOrDefault(_customer => { return _customer.CustomerID == line.CustomerID; });
                if (customer == null)
                    Debug.WriteLine("WARNING: Undefined customer found in transaction: " + line.CustomerID);
            }
            AuditFileMasterFilesSupplier? supplier = null;
            if (!string.IsNullOrEmpty(line.SupplierID))
            {
                supplier = Suppliers.FirstOrDefault(_supplier => { return _supplier.SupplierID == line.SupplierID; });
                if (supplier == null)
                    Debug.WriteLine("WARNING: Undefined supplier found in transaction: " + line.SupplierID);
            }

            if (line.ItemElementName == ItemChoiceType4.CreditAmount)
            { 
                if (account != null)
                {
                    decimal previousBalance = account.Item1ElementName == Item1ChoiceType.ClosingDebitBalance ? account.Item1 : -account.Item1;
                    previousBalance -= line.Item.Amount;
                    account.Item1 = Math.Abs(previousBalance);
                    account.Item1ElementName = previousBalance >= 0 ? Item1ChoiceType.ClosingDebitBalance : Item1ChoiceType.ClosingCreditBalance;
                }
                if (customer != null)
                {
                    decimal previousBalance = customer.Item1ElementName == Item1ChoiceType1.ClosingDebitBalance ? customer.Item1 : -customer.Item1;
                    previousBalance -= line.Item.Amount;
                    customer.Item1 = Math.Abs(previousBalance);
                    customer.Item1ElementName = previousBalance >= 0 ? Item1ChoiceType1.ClosingDebitBalance : Item1ChoiceType1.ClosingCreditBalance;
                }
                if (supplier != null)
                {
                    decimal previousBalance = supplier.Item1ElementName == Item1ChoiceType2.ClosingDebitBalance ? supplier.Item1 : -supplier.Item1;
                    previousBalance -= line.Item.Amount;
                    supplier.Item1 = Math.Abs(previousBalance);
                    supplier.Item1ElementName = previousBalance >= 0 ? Item1ChoiceType2.ClosingDebitBalance : Item1ChoiceType2.ClosingCreditBalance;
                }
            }
            else // Debit
            {
                if (account != null)
                {
                    decimal previousBalance = account.Item1ElementName == Item1ChoiceType.ClosingDebitBalance ? account.Item1 : -account.Item1;
                    previousBalance += line.Item.Amount;
                    account.Item1 = Math.Abs(previousBalance);
                    account.Item1ElementName = previousBalance >= 0 ? Item1ChoiceType.ClosingDebitBalance : Item1ChoiceType.ClosingCreditBalance;
                }
                if (customer != null)
                {
                    decimal previousBalance = customer.Item1ElementName == Item1ChoiceType1.ClosingDebitBalance ? customer.Item1 : -customer.Item1;
                    previousBalance += line.Item.Amount;
                    customer.Item1 = Math.Abs(previousBalance);
                    customer.Item1ElementName = previousBalance >= 0 ? Item1ChoiceType1.ClosingDebitBalance : Item1ChoiceType1.ClosingCreditBalance;
                }
                if (supplier != null)
                {
                    decimal previousBalance = supplier.Item1ElementName == Item1ChoiceType2.ClosingDebitBalance ? supplier.Item1 : -supplier.Item1;
                    previousBalance += line.Item.Amount;
                    supplier.Item1 = Math.Abs(previousBalance);
                    supplier.Item1ElementName = previousBalance >= 0 ? Item1ChoiceType2.ClosingDebitBalance : Item1ChoiceType2.ClosingCreditBalance;
                }
            }
        }
    }

    /// <summary>
    /// Recalculates and fills out the various total credit and debit balances in the books, such as account, customer and supplier closing balances.
    /// </summary>
    private void RecalculateBalances()
    {
        var totalDebit = 0m;
        var totalCredit = 0m;
        foreach (var account in Accounts)
        {
            account.HelperClosingBalance = account.ItemElementName == ItemChoiceType.OpeningDebitBalance ? account.Item : -account.Item;
        }
        foreach (var customer in Customers)
        {
            customer.HelperClosingBalance = customer.ItemElementName == ItemChoiceType1.OpeningDebitBalance ? customer.Item : -customer.Item;
        }
        foreach (var supplier in Suppliers)
        {
            supplier.HelperClosingBalance = supplier.ItemElementName == ItemChoiceType2.OpeningDebitBalance ? supplier.Item : -supplier.Item;
        }

        foreach (var transaction in Transactions)
        {
            foreach (var line in transaction.Line)
            {
                AuditFileMasterFilesAccount? account = null;
                if (!string.IsNullOrEmpty(line.AccountID))
                {
                    account = Accounts.FirstOrDefault(_account => { return _account.AccountID == line.AccountID; });
                    if (account == null)
                        Debug.WriteLine("WARNING: Undefined account found in transaction: " + line.AccountID);
                }
                AuditFileMasterFilesCustomer? customer = null;
                if (!string.IsNullOrEmpty(line.CustomerID))
                {
                    customer = Customers.FirstOrDefault(_customer => { return _customer.CustomerID == line.CustomerID; });
                    if (customer == null)
                        Debug.WriteLine("WARNING: Undefined customer found in transaction: " + line.CustomerID);
                }
                AuditFileMasterFilesSupplier? supplier = null;
                if (!string.IsNullOrEmpty(line.SupplierID))
                {
                    supplier = Suppliers.FirstOrDefault(_supplier => { return _supplier.SupplierID == line.SupplierID; });
                    if (supplier == null)
                        Debug.WriteLine("WARNING: Undefined supplier found in transaction: " + line.SupplierID);
                }

                if (line.ItemElementName == ItemChoiceType4.CreditAmount)
                {
                    totalCredit += line.Item.Amount;
                    if (account != null)
                        account.HelperClosingBalance -= line.Item.Amount;
                    if (customer != null)
                        customer.HelperClosingBalance -= line.Item.Amount;
                    if (supplier != null)
                        supplier.HelperClosingBalance -= line.Item.Amount;
                }
                else // Debit
                {
                    totalDebit += line.Item.Amount;
                    if (account != null)
                        account.HelperClosingBalance += line.Item.Amount;
                    if (customer != null)
                        customer.HelperClosingBalance += line.Item.Amount;
                    if (supplier != null)
                        supplier.HelperClosingBalance += line.Item.Amount;
                }
            }
        }

        foreach (var account in Accounts)
        {
            account.Item1 = Math.Abs(account.HelperClosingBalance);
            account.Item1ElementName = account.HelperClosingBalance >= 0 ? Item1ChoiceType.ClosingDebitBalance : Item1ChoiceType.ClosingCreditBalance;
        }
        foreach (var customer in Customers)
        {
            customer.Item1 = Math.Abs(customer.HelperClosingBalance);
            customer.Item1ElementName = customer.HelperClosingBalance >= 0 ? Item1ChoiceType1.ClosingDebitBalance : Item1ChoiceType1.ClosingCreditBalance;
        }
        foreach (var supplier in Suppliers)
        {
            supplier.Item1 = Math.Abs(supplier.HelperClosingBalance);
            supplier.Item1ElementName = supplier.HelperClosingBalance >= 0 ? Item1ChoiceType2.ClosingDebitBalance : Item1ChoiceType2.ClosingCreditBalance;
        }

        books.GeneralLedgerEntries.TotalDebit = totalDebit;
        books.GeneralLedgerEntries.TotalCredit = totalCredit;

        if (totalDebit != totalCredit)
            Debug.WriteLine("WARNING: Total credit and debit in books does not cancel out");
    }

    /// <summary>
    /// Gets the underlying SAF-T data, for manual modification. Be careful not to break stuff.
    /// </summary>
    /// <returns>SAF-T format books</returns>
    public AuditFile? GetRawBooks()
    {
        return books;
    }


    public void Load(string filename)
    {
        if (Saft.SaftHelper.Deserialize(filename) is not AuditFile loadedBooks)
            throw new Exception("Could not load SAF-T file properly, result was empty.");

        books = loadedBooks;

        Transactions = books.GeneralLedgerEntries.Journal.First().Transaction.ToList();
        Customers = books.MasterFiles.Customers.ToList();
        Suppliers = books.MasterFiles.Suppliers.ToList();
        TaxClasses = books.MasterFiles.TaxTable.ToList();
        Accounts = books.MasterFiles.GeneralLedgerAccounts.ToList();

        nextSourceDocumentId = 1;
        nextTransactionId = 1;
        foreach (var transaction in Transactions)
        {
            if (int.TryParse(transaction.TransactionID, out int transactionId) && transactionId >= nextTransactionId)
                nextTransactionId = transactionId + 1;
            if (int.TryParse(transaction.Line.First().SourceDocumentID, out int sourceDocumentId) && sourceDocumentId >= nextSourceDocumentId)
                nextSourceDocumentId = sourceDocumentId + 1;
        }
        nextAccountId = 1;
        nextCustomerId = 1;
        foreach (var customer in Customers)
        {
            if (int.TryParse(customer.CustomerID, out int customerId) && customerId >= nextCustomerId)
                nextCustomerId = customerId + 1;
            if (int.TryParse(customer.AccountID, out int accountId) && accountId >= nextAccountId)
                nextAccountId = accountId + 1;
        }
        nextSupplierId = 1;
        foreach (var supplier in Suppliers)
        {
            if (int.TryParse(supplier.SupplierID, out int supplierId) && supplierId >= nextSupplierId)
                nextSupplierId = supplierId + 1;
            if (int.TryParse(supplier.AccountID, out int accountId) && accountId >= nextAccountId)
                nextAccountId = accountId + 1;
        }
    }

    public void Save(string filename)
    {
        if (books == null)
            throw new Exception("Books must be initialized with Load() or GenerateDefaultEmpty() first");

        RecalculateBalances();

        books.MasterFiles.Customers = Customers.ToArray();
        books.MasterFiles.Suppliers = Suppliers.ToArray();
        books.GeneralLedgerEntries.Journal.First().Transaction = Transactions.ToArray();
        books.MasterFiles.GeneralLedgerAccounts = Accounts.ToArray();
        books.MasterFiles.TaxTable = TaxClasses.ToArray();

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
        AddDefaultTaxClasses();

        books.GeneralLedgerEntries = new();
        var journal = new AuditFileGeneralLedgerEntriesJournal
        {
            Description = "Bok",
            JournalID = "GL",
            Type = "GL",
        };
        books.GeneralLedgerEntries.Journal = new Saft.AuditFileGeneralLedgerEntriesJournal[1] { journal };

    }

    private void AddDefaultTaxClasses()
    {
        if (books == null)
            throw new Exception("Books must be initialized with Load() or GenerateDefaultEmpty() first");

        TaxClasses = new()
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
    }

    private void AddDefaultAccounts()
    {
        if (books == null)
            throw new Exception("Books must be initialized with Load() or GenerateDefaultEmpty() first");

        Accounts = new List<AuditFileMasterFilesAccount>();
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "1420",
            StandardAccountID = "14",
            AccountDescription = "Lager varer/deler",
            ItemElementName = ItemChoiceType.OpeningDebitBalance,
            Item = 174_606.23m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "1501",
            StandardAccountID = "15",
            AccountDescription = "Kunder, Paypal-betaling",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 2109.12m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "1502",
            StandardAccountID = "15",
            AccountDescription = "Kunder, Stripe-betaling",
            ItemElementName = ItemChoiceType.OpeningDebitBalance,
            Item = 27808.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "1503",
            StandardAccountID = "15",
            AccountDescription = "Kunder, Ebay-betaling",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "1504",
            StandardAccountID = "15",
            AccountDescription = "Kunder, via Amazon",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "1505",
            StandardAccountID = "15",
            AccountDescription = "Kunder, andre",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "2060",
            StandardAccountID = "20",
            AccountDescription = "Privat uttak/utlegg",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "2400",
            StandardAccountID = "24",
            AccountDescription = "Leverandører",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 304.65m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "2700",
            StandardAccountID = "27",
            AccountDescription = "Utgående MVA",
            ItemElementName = ItemChoiceType.OpeningDebitBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "2710",
            StandardAccountID = "27",
            AccountDescription = "Inngående MVA",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "2740",
            StandardAccountID = "27",
            AccountDescription = "Oppgjør MVA",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "3000",
            StandardAccountID = "30",
            AccountDescription = "Salg, Høy MVA-sats",
            ItemElementName = ItemChoiceType.OpeningDebitBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "3100",
            StandardAccountID = "31",
            AccountDescription = "Salg, MVA-fritatt",
            ItemElementName = ItemChoiceType.OpeningDebitBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "3705",
            StandardAccountID = "37",
            AccountDescription = "Provisjon, MVA-fritatt",
            ItemElementName = ItemChoiceType.OpeningDebitBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "3900",
            StandardAccountID = "39",
            AccountDescription = "Andre inntekter",
            ItemElementName = ItemChoiceType.OpeningDebitBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "4100",
            StandardAccountID = "41",
            AccountDescription = "Forbruk varer/deler",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "4160",
            StandardAccountID = "41",
            AccountDescription = "Frakt/toll innkjøp varer/deler",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "6100",
            StandardAccountID = "61",
            AccountDescription = "Fraktkostnader",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "6100",
            StandardAccountID = "61",
            AccountDescription = "Toll- og spedisjonskostnader",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "6540",
            StandardAccountID = "65",
            AccountDescription = "Forbruk inventar",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "6552",
            StandardAccountID = "65",
            AccountDescription = "Forbruk programvare",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "6560",
            StandardAccountID = "65",
            AccountDescription = "Forbruk rekvisita",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "6590",
            StandardAccountID = "65",
            AccountDescription = "Forbruk annet",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "6901",
            StandardAccountID = "69",
            AccountDescription = "Telefonkostnader",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "6907",
            StandardAccountID = "69",
            AccountDescription = "Internettkostnader",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "7300",
            StandardAccountID = "73",
            AccountDescription = "Salgskostnader",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "7320",
            StandardAccountID = "73",
            AccountDescription = "Markedsføringskostnader",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "7550",
            StandardAccountID = "75",
            AccountDescription = "Garanti- og returkostnad",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "7770",
            StandardAccountID = "77",
            AccountDescription = "Betalingsgebyr",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "7798",
            StandardAccountID = "77",
            AccountDescription = "Andre kostnader",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "8060",
            StandardAccountID = "80",
            AccountDescription = "Valutagevinst",
            ItemElementName = ItemChoiceType.OpeningDebitBalance,
            Item = 0.00m,
        });
        Accounts.Add(new AuditFileMasterFilesAccount()
        {
            AccountID = "8160",
            StandardAccountID = "81",
            AccountDescription = "Valutatap",
            ItemElementName = ItemChoiceType.OpeningCreditBalance,
            Item = 0.00m,
        });
    }
}

public class TransactionLine
{
    public decimal Amount { get; set; }
    public decimal? TaxBase { get; set; }
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
    public TransactionLine(decimal amount, string accountId, decimal? taxBase = null, string description = "", string? currency = null, string? taxCode = null, string? customerId = null, string? supplierId = null)
    {
        Amount = amount;
        AccountId = accountId;
        TaxBase = taxBase;
        Description = description;
        Currency = string.IsNullOrWhiteSpace(currency) ? null : currency;
        CustomerId = string.IsNullOrWhiteSpace(customerId) ? null : customerId;
        SupplierId = string.IsNullOrWhiteSpace(supplierId) ? null : supplierId;
        TaxCode = string.IsNullOrWhiteSpace(taxCode) ? null : taxCode;

        if (CustomerId != null && SupplierId != null)
            throw new Exception("Cannot specify both customer ID and supplier ID in a transaction line");
    }
}

public struct EntityId
{
    public string CustomerSupplierId { get; }
    public string AccountId { get; }

    public EntityId(string customerSupplierId, string accountId)
    {
        CustomerSupplierId = customerSupplierId;
        AccountId = accountId;
    }
}

public class StandardTaxCodes
{
    public const string NoTax = "0";
    public const string IncomingPurchaseTaxHighRate = "1";
    public const string OutgoingSaleTaxHighRate = "3";
    public const string ImportHighRate_SupplierInvoice = "21";
    public const string ImportHighRate_Tax = "81";
    public const string Export = "52";
}
