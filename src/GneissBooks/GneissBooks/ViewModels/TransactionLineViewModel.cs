using CommunityToolkit.Mvvm.ComponentModel;
using GneissBooks.Saft;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GneissBooks.ViewModels
{
    public partial class TransactionLineViewModel : ViewModelBase
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AmountNumeric))]
        private string _amount = "0";
        [ObservableProperty]
        private string _accountId = "";
        [ObservableProperty]
        private string? _supplierId;
        [ObservableProperty]
        private string? _customerId;
        [ObservableProperty]
        private string _description = "";
        [ObservableProperty]
        private string? _currency;
        [ObservableProperty]
        private string? _currencyExchangeRate;
        [ObservableProperty]
        private int _taxCodeSelectionIndex = 0;

        public string? TaxCode => TaxCodeItems[TaxCodeSelectionIndex].TaxCode;

        static public List<TaxClass> TaxCodeItems { get; } = new() // Todo get from books
        {
            new TaxClass("N/A", null),
            new TaxClass("Zero rate", StandardTaxCodes.NoTax),
            new TaxClass("Incoming (from purchase), high rate", StandardTaxCodes.IncomingPurchaseTaxHighRate),
            new TaxClass("Outgoing (from sale), high rate", StandardTaxCodes.OutgoingSaleTaxHighRate),
            new TaxClass("Import, high rate, supplier invoice", StandardTaxCodes.ImportHighRate_SupplierInvoice),
            new TaxClass("Import, high rate, tax", StandardTaxCodes.ImportHighRate_Tax),
            new TaxClass("Export, zero rate", StandardTaxCodes.Export)
        };

        static public ObservableCollection<Customer> CustomerItems { get; } = new() // Todo get from books
        {
            new Customer("test", "10"),
            new Customer("tsest2", "11")
        };

        public decimal AmountNumeric => decimal.TryParse(Amount, out decimal amountNumeric) ? amountNumeric : 0m;

        public TransactionLineViewModel()
        {
        }

        public TransactionLineViewModel(AuditFileGeneralLedgerEntriesJournalTransactionLine rawLine) 
        {
            var amount = (rawLine.Item.CurrencyAmount == null || (rawLine.Item.CurrencyAmount == 0 && rawLine.Item.Amount != 0)) ? rawLine.Item.Amount : rawLine.Item.CurrencyAmount;
            Amount = (rawLine.ItemElementName == ItemChoiceType4.CreditAmount ? amount : -amount).ToString(CultureInfo.InvariantCulture);
            AccountId = rawLine.AccountID;
            SupplierId = rawLine.SupplierID;
            CustomerId = rawLine.CustomerID;
            Description = rawLine.Description;
            Currency = rawLine.Item.CurrencyCode;
            CurrencyExchangeRate = rawLine.Item.ExchangeRate.ToString(CultureInfo.InvariantCulture);
            TaxCodeSelectionIndex = TaxCodeItems.FindIndex((item) => { return item.TaxCode == rawLine.TaxInformation?.FirstOrDefault()?.TaxCode; });
        }
    }

    
}
