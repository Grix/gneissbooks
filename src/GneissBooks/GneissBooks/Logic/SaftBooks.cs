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
using System.Threading;
using System.Threading.Tasks;

namespace GneissBooks;

/// <summary>
/// Basic accounting system, wrapped around the SAF-T file format (Norwegian dialect)
/// </summary>
public class SaftBooks
{
    public List<AuditFileMasterFilesCustomer> Customers { get; private set; } = new();
    public List<AuditFileMasterFilesSupplier> Suppliers { get; private set; } = new();
    public List<AuditFileGeneralLedgerEntriesJournalTransaction> Transactions { get; private set; } = new();
    public List<AuditFileMasterFilesAccount> Accounts { get; private set; } = new();
    public List<AuditFileMasterFilesTaxTableEntry> TaxClasses { get; private set; } = new();

    int nextSourceDocumentId = 1;
    int nextTransactionId = 1;
    int nextCustomerId = 1000;
    int nextSupplierId = 1;

    const decimal highTaxRate = 25m;

    Saft.AuditFile books;

    public SaftBooks()
    {
        books = new();
        GenerateDefaultEmpty();
    }

    /// <summary>
    /// Creates, finds or modifies a customer. If customerId is specified, first check if it is an existing customer, and if so, modify it with the information from the other parameters rather than make a new one.
    /// If customerId is not specified, first try to find an existing customer roughly matching the information in the other parameters, and if found, return it (without modifying its content).
    /// If no existing customer could be found, create a new one with the given information. Information that is not specified will be either empty or assigned automatically. 
    /// </summary>
    /// <param name="companyName"></param>
    /// <param name="firstName"></param>
    /// <param name="lastName"></param>
    /// <param name="postCode"></param>
    /// <param name="city"></param>
    /// <param name="streetName"></param>
    /// <param name="streetNumber"></param>
    /// <param name="addressLine2"></param>
    /// <param name="countryCode"></param>
    /// <param name="telephone"></param>
    /// <param name="email"></param>
    /// <param name="startingBalance"></param>
    /// <param name="customerId"></param>
    /// <returns>Customer ID for the new or found customer.</returns>
    /// <exception cref="Exception"></exception>
    public async Task<string> FindOrAddCustomer(string? companyName, string? firstName, string? lastName, string? postCode, string? city, string? streetName, 
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
                customer.Name = string.IsNullOrWhiteSpace(companyName) ? $"{lastName}, {firstName}" : companyName;
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

                return customer.CustomerID;
            }
        }
        else
        {
            // Search for similar existing and simply return the ID if found, not modifying it
            foreach (var customer in Customers)
            {
                var similarity = 0.0;

                if (string.IsNullOrEmpty(telephone))
                    similarity += 0.5;
                else if (customer.Contact?.FirstOrDefault()?.Telephone == telephone)
                    similarity += 2;
                if (!string.IsNullOrWhiteSpace(email) && customer.Contact?.FirstOrDefault()?.Email == email)
                    similarity += 2;
                if (!string.IsNullOrWhiteSpace(companyName) && customer.Name.ToLower() == companyName.ToLower())
                    similarity += 2;

                if (similarity >= 3)
                    return customer.CustomerID;

                if (string.IsNullOrEmpty(postCode))
                    similarity += 0.5;
                else if (customer.Address?.FirstOrDefault()?.PostalCode == postCode)
                    similarity += 1;
                if (firstName != null && customer.Contact?.FirstOrDefault()?.ContactPerson?.FirstName.ToLower() == firstName.ToLower())
                    similarity += 1;
                if (lastName != null && customer.Contact?.FirstOrDefault()?.ContactPerson?.LastName.ToLower() == lastName.ToLower())
                    similarity += 1;

                if (similarity >= 3)
                    return customer.CustomerID;
            }
        }

        // No existing found, create new
        var newCustomer = new AuditFileMasterFilesCustomer()
        {
            CustomerID = string.IsNullOrWhiteSpace(customerId) ? (nextCustomerId++.ToString()) : customerId,
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

        return newCustomer.CustomerID;
    }

    /// <summary>
    /// Creates, finds or modifies a supplier. If supplierId is specified, first check if it is an existing supplier, and if so, modify it with the information from the other parameters rather than make a new one.
    /// If supplierId is not specified, first try to find an existing supplier roughly matching the information in the other parameters, and if found, return it (without modifying its content).
    /// If no existing supplier could be found, create a new one with the given information. Information that is not specified will be either empty or assigned automatically. 
    /// </summary>
    /// <param name="companyName"></param>
    /// <param name="firstName"></param>
    /// <param name="lastName"></param>
    /// <param name="postCode"></param>
    /// <param name="city"></param>
    /// <param name="streetName"></param>
    /// <param name="streetNumber"></param>
    /// <param name="addressLine2"></param>
    /// <param name="countryCode"></param>
    /// <param name="telephone"></param>
    /// <param name="email"></param>
    /// <param name="startingBalance"></param>
    /// <param name="supplierId"></param>
    /// <returns>Supplier ID for the new or found supplier.</returns>
    /// <exception cref="Exception"></exception>
    public async Task<string> FindOrAddSupplier(string? companyName, string? firstName, string? lastName, string? postCode, string? city, string? streetName,
                                                    string? streetNumber, string? addressLine2, string? countryCode, string? telephone, string? email, decimal startingBalance = 0m, string? supplierId = null)
    {
        // todo refactor this to reuse code from FindOrAddCustomer()

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

                return supplier.SupplierID;
            }
        }
        else
        {
            // Search for similar existing and simply return the ID if found, not modifying it
            foreach (var supplier in Suppliers)
            {
                var similarity = 0.0;

                if (string.IsNullOrEmpty(telephone))
                    similarity += 0.5;
                else if (supplier.Contact?.FirstOrDefault()?.Telephone == telephone)
                    similarity += 2;
                if (email != null && supplier.Contact?.FirstOrDefault()?.Email == email)
                    similarity += 2;
                if (companyName != null && supplier.Name == companyName)
                    similarity += 2;

                if (similarity >= 2)
                    return supplier.SupplierID;

                if (string.IsNullOrEmpty(postCode))
                    similarity += 0.5;
                else if (supplier.Address?.FirstOrDefault()?.PostalCode == postCode)
                    similarity += 1;
                if (firstName != null && supplier.Contact?.FirstOrDefault()?.ContactPerson?.FirstName == firstName)
                    similarity += 1;
                if (lastName != null && supplier.Contact?.FirstOrDefault()?.ContactPerson?.LastName == lastName)
                    similarity += 1;

                if (similarity >= 2)
                    return supplier.SupplierID;
            }
        }

        // No existing found, create new
        var newSupplier = new AuditFileMasterFilesSupplier()
        {
            SupplierID = string.IsNullOrWhiteSpace(supplierId) ? (nextSupplierId++.ToString()) : supplierId,
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

        return newSupplier.SupplierID;
    }

    /// <summary>
    /// Adds a transaction to the ledger.
    /// </summary>
    /// <param name="date">Time the transaction took place.</param>
    /// <param name="description">Text description of the transaction.</param>
    /// <param name="lines">Individual amounts on the ledger, for one account each.</param>
    /// <returns>Source document ID. The file should be renamed to, or otherwise stored with, this new reference.</returns>
    public async Task<string> AddTransaction(DateTimeOffset date, string description, IEnumerable<TransactionLine> lines)
    {
        var formattedLines = new List<AuditFileGeneralLedgerEntriesJournalTransactionLine>();
        int recordId = 1;
        var sourceDocumentId = (nextSourceDocumentId++).ToString();
        var totalSum = 0m;
        var hasCurrencyExchange = false;
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

            if (!string.IsNullOrWhiteSpace(line.Currency))
            {
                // Convert currency
                formattedLine.Item.CurrencyAmount = Math.Abs(line.Amount);
                var exchangeRate = await ExchangeRateApi.GetExchangeRateInNok(line.Currency, DateOnly.FromDateTime(date.Date));
                formattedLine.Item.Amount = Math.Round(formattedLine.Item.CurrencyAmount * exchangeRate, 2);
                formattedLine.Item.ExchangeRate = Math.Round(exchangeRate, 7);
                formattedLine.Item.ExchangeRateSpecified = true;
                formattedLine.Item.CurrencyCode = line.Currency;
                formattedLine.Item.CurrencyAmount = Math.Round(formattedLine.Item.CurrencyAmount, 2);
                hasCurrencyExchange = true;
            }
            else
            {
                formattedLine.Item.Amount = Math.Round(Math.Abs(line.Amount), 2);
                formattedLine.Item.ExchangeRateSpecified = false;
            }

            totalSum += (formattedLine.ItemElementName == ItemChoiceType4.DebitAmount) ? formattedLine.Item.Amount : -formattedLine.Item.Amount;

            if (hasCurrencyExchange)
            {
                // Workaround for currency exchange rounding errors
                if (Math.Abs(totalSum) < 0.02m && Math.Abs(totalSum) > 0m)
                {
                    Debug.WriteLine("Warning: Currency exchange rounding error detected. Adjusting " + totalSum.ToString("N2"));

                    AuditFileGeneralLedgerEntriesJournalTransactionLine previousCurrencyExchangeLine;
                    if (!string.IsNullOrWhiteSpace(line.Currency))
                        previousCurrencyExchangeLine = formattedLine;
                    else
                        previousCurrencyExchangeLine = formattedLines.Last(_line => _line.Item.CurrencyAmountSpecified);

                    previousCurrencyExchangeLine.Item.Amount += (formattedLine.ItemElementName == ItemChoiceType4.DebitAmount) ? -totalSum : totalSum;
                    totalSum = 0;
                }
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

                    taxInfo.TaxAmount = new AmountStructure()
                    {
                        Amount = Math.Round((line.TaxBase ?? Math.Abs(formattedLine.Item.Amount)) * taxInfo.TaxPercentage / 100m, 2) 
                    };
                    formattedLine.TaxInformation = new TaxInformationStructure[] { taxInfo };
                }
            }

            formattedLines.Add(formattedLine);
        }

        if (totalSum != 0m)
            throw new Exception("Trying to add invalid transaction: Debit and credit does not cancel out");

        var index = 0;
        while (index < Transactions.Count) 
        {
            if (Transactions[index].TransactionDate > date)
                break;

            index++;
        }

        // todo make sure date is always correct. There is some time zone shenanigans.
        var transaction = new AuditFileGeneralLedgerEntriesJournalTransaction()
        {
            TransactionID = (nextTransactionId++).ToString(),
            Period = date.Date.Month.ToString(),
            PeriodYear = date.Date.Year.ToString(),
            TransactionDate = date.Date,
            Description = description,
            SystemEntryDate = DateTime.Now,
            GLPostingDate = date.Date,
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
    public void RecalculateBalances()
    {
        var totalDebit = 0m;
        var totalCredit = 0m;
        var numberOfEntries = 0;
        foreach (var account in Accounts)
        {
            account.HelperClosingBalance = (account.ItemElementName == ItemChoiceType.OpeningDebitBalance) ? account.Item : -account.Item;
        }
        foreach (var customer in Customers)
        {
            customer.HelperClosingBalance = (customer.ItemElementName == ItemChoiceType1.OpeningDebitBalance) ? customer.Item : -customer.Item;
        }
        foreach (var supplier in Suppliers)
        {
            supplier.HelperClosingBalance = (supplier.ItemElementName == ItemChoiceType2.OpeningDebitBalance) ? supplier.Item : -supplier.Item;
        }

        foreach (var transaction in Transactions)
        {
            var totalTransactionDebit = 0m;
            var totalTransactionCredit = 0m;
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

                if (line.Item.Amount > 500000 || line.Item.Amount < 0)
                {
                    Debug.WriteLine("WARNING: Suspicious amount " + line.Item.Amount + " found in transaction: " + line.SupplierID);
                }

                if (line.ItemElementName == ItemChoiceType4.CreditAmount)
                {
                    totalCredit += line.Item.Amount;
                    totalTransactionCredit += line.Item.Amount;
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
                    totalTransactionDebit += line.Item.Amount;
                    if (account != null)
                        account.HelperClosingBalance += line.Item.Amount;
                    if (customer != null)
                        customer.HelperClosingBalance += line.Item.Amount;
                    if (supplier != null)
                        supplier.HelperClosingBalance += line.Item.Amount;
                }
            }
            numberOfEntries++;
            if (totalTransactionDebit != totalTransactionCredit)
                Debug.WriteLine("WARNING: Total credit and debit in transaction " + transaction.TransactionID + " does not cancel out.");
        }

        foreach (var account in Accounts)
        {
            account.Item1 = Math.Abs(account.HelperClosingBalance);
            account.Item1ElementName = (account.HelperClosingBalance >= 0) ? Item1ChoiceType.ClosingDebitBalance : Item1ChoiceType.ClosingCreditBalance;
        }
        foreach (var customer in Customers)
        {
            customer.Item1 = Math.Abs(customer.HelperClosingBalance);
            customer.Item1ElementName = (customer.HelperClosingBalance >= 0) ? Item1ChoiceType1.ClosingDebitBalance : Item1ChoiceType1.ClosingCreditBalance;
        }
        foreach (var supplier in Suppliers)
        {
            supplier.Item1 = Math.Abs(supplier.HelperClosingBalance);
            supplier.Item1ElementName = (supplier.HelperClosingBalance >= 0) ? Item1ChoiceType2.ClosingDebitBalance : Item1ChoiceType2.ClosingCreditBalance;
        }

        books.GeneralLedgerEntries.NumberOfEntries = numberOfEntries.ToString(CultureInfo.InvariantCulture);
        books.GeneralLedgerEntries.TotalDebit = totalDebit;
        books.GeneralLedgerEntries.TotalCredit = totalCredit;

        if (totalDebit != totalCredit)
            Debug.WriteLine("WARNING: Total credit and debit in books does not cancel out.");
    }

    /// <summary>
    /// Gets the underlying SAF-T data, for manual modification. Be careful not to break stuff.
    /// </summary>
    /// <returns>SAF-T format books</returns>
    public AuditFile? GetRawBooks()
    {
        return books;
    }


    /// <summary>
    /// Loads the books from a given SAF-T file.
    /// </summary>
    /// <param name="filename"></param>
    /// <exception cref="Exception"></exception>
    public void Load(string filename)
    {
        if (Saft.SaftHelper.Deserialize(filename) is not AuditFile loadedBooks)
            throw new Exception("Could not load SAF-T file properly, result was empty.");

        books = loadedBooks;

        books.Header ??= new();
        books.MasterFiles ??= new();
        books.GeneralLedgerEntries ??= new();
        if (books.GeneralLedgerEntries?.Journal == null || books.GeneralLedgerEntries.Journal.Count() == 0)
        {
            var journal = new AuditFileGeneralLedgerEntriesJournal
            {
                Description = "Bok",
                JournalID = "GL",
                Type = "GL",
            };
            books.GeneralLedgerEntries!.Journal = new Saft.AuditFileGeneralLedgerEntriesJournal[1] { journal };
        }

        Transactions = books.GeneralLedgerEntries.Journal.First().Transaction?.ToList() ?? new();
        Customers = books.MasterFiles?.Customers?.ToList() ?? new();
        Suppliers = books.MasterFiles?.Suppliers?.ToList() ?? new();
        TaxClasses = books.MasterFiles?.TaxTable?.ToList() ?? new();
        Accounts = books.MasterFiles?.GeneralLedgerAccounts?.ToList() ?? new();

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

    /// <summary>
    /// Saves the current books to a SAF-T file.
    /// </summary>
    /// <param name="filename"></param>
    /// <exception cref="Exception"></exception>
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

        books.Header.AuditFileVersion = "1.0";
        books.Header.SoftwareCompanyName = "Mikkelsen Innovasjon";
        books.Header.SoftwareID = Assembly.GetExecutingAssembly().GetName().Name ?? "GneissBooks";
        books.Header.SoftwareVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "?";

        Saft.SaftHelper.Serialize(books, filename);

        Debug.WriteLine("Finished exporting SAF-T file.");
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
                    TaxRegistrationNumber = "923589155MVA",
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
                        FirstName = "Gitle",
                        LastName = "Mikkelsen",
                    },
                    Telephone = "47639451",
                    Email = "gitlem@gmail.com",
                }
            }
        };
        books.Header.AuditFileCountry = "NO";
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
                        Description = "0: Ingen avgifter",
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
                        Description = "1: Inngående avgift (kjøp), høy sats",
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
                        Description = "3: Utgående avgift (salg), høy sats",
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
                        TaxCode = "14",
                        Description = "14: Innførsel av varer, høy sats. Betalt ved innførsel.",
                        Item = highTaxRate,
                        StandardTaxCode = "14",
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
                        Description = "21: Innførsel av varer, høy sats. Leverandørfaktura.",
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
                        Description = "81: Innførsel av varer, høy sats. Grunnlag til MVA-melding.",
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
                        Description = "52: Utførsel av varer og tjenester, 0%",
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
        Accounts =
        [
            new AuditFileMasterFilesAccount()
            {
                AccountID = "1420",
                StandardAccountID = "14",
                AccountDescription = "Lager varer/deler",
                ItemElementName = ItemChoiceType.OpeningDebitBalance,
                Item = 174_606.23m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "1501",
                StandardAccountID = "15",
                AccountDescription = "Kunder, Paypal-betaling",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 2109.12m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "1502",
                StandardAccountID = "15",
                AccountDescription = "Kunder, Stripe-betaling",
                ItemElementName = ItemChoiceType.OpeningDebitBalance,
                Item = 5511.28m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "1503",
                StandardAccountID = "15",
                AccountDescription = "Kunder, Ebay-betaling",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "1504",
                StandardAccountID = "15",
                AccountDescription = "Kunder, via Amazon",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "1505",
                StandardAccountID = "15",
                AccountDescription = "Kunder, andre",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "2060",
                StandardAccountID = "20",
                AccountDescription = "Privat uttak/utlegg",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "2400",
                StandardAccountID = "24",
                AccountDescription = "Leverandører",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 304.65m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "2700",
                StandardAccountID = "27",
                AccountDescription = "Utgående MVA",
                ItemElementName = ItemChoiceType.OpeningDebitBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "2710",
                StandardAccountID = "27",
                AccountDescription = "Inngående MVA",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "2740",
                StandardAccountID = "27",
                AccountDescription = "Oppgjør MVA",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "3000",
                StandardAccountID = "30",
                AccountDescription = "Salg, Høy MVA-sats",
                ItemElementName = ItemChoiceType.OpeningDebitBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "3100",
                StandardAccountID = "31",
                AccountDescription = "Salg, MVA-fritatt",
                ItemElementName = ItemChoiceType.OpeningDebitBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "3705",
                StandardAccountID = "37",
                AccountDescription = "Provisjon, MVA-fritatt",
                ItemElementName = ItemChoiceType.OpeningDebitBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "3900",
                StandardAccountID = "39",
                AccountDescription = "Andre inntekter",
                ItemElementName = ItemChoiceType.OpeningDebitBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "4100",
                StandardAccountID = "41",
                AccountDescription = "Forbruk varer/deler",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "4160",
                StandardAccountID = "41",
                AccountDescription = "Frakt/toll,  innkjøp",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "6100",
                StandardAccountID = "61",
                AccountDescription = "Frakt/toll, utsending",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "6540",
                StandardAccountID = "65",
                AccountDescription = "Forbruk inventar",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "6552",
                StandardAccountID = "65",
                AccountDescription = "Forbruk programvare",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "6560",
                StandardAccountID = "65",
                AccountDescription = "Forbruk rekvisita",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "6590",
                StandardAccountID = "65",
                AccountDescription = "Forbruk annet",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "6901",
                StandardAccountID = "69",
                AccountDescription = "Telefonkostnader",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "6907",
                StandardAccountID = "69",
                AccountDescription = "Internettkostnader",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "7300",
                StandardAccountID = "73",
                AccountDescription = "Salgskostnader",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "7320",
                StandardAccountID = "73",
                AccountDescription = "Markedsføringskostnader",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "7550",
                StandardAccountID = "75",
                AccountDescription = "Garanti- og returkostnad",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "7770",
                StandardAccountID = "77",
                AccountDescription = "Betalingsgebyr",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "7798",
                StandardAccountID = "77",
                AccountDescription = "Andre kostnader",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "8060",
                StandardAccountID = "80",
                AccountDescription = "Valutagevinst",
                ItemElementName = ItemChoiceType.OpeningDebitBalance,
                Item = 0.00m,
            },
            new AuditFileMasterFilesAccount()
            {
                AccountID = "8160",
                StandardAccountID = "81",
                AccountDescription = "Valutatap",
                ItemElementName = ItemChoiceType.OpeningCreditBalance,
                Item = 0.00m,
            },
        ];
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

public class StandardTaxCodes
{
    public const string NoTax = "0";
    public const string IncomingPurchaseTaxHighRate = "1";
    public const string OutgoingSaleTaxHighRate = "3";
    public const string ImportHighRate_SupplierInvoice = "21";
    public const string ImportHighRate_Tax = "81";
    public const string Export = "52";
}
