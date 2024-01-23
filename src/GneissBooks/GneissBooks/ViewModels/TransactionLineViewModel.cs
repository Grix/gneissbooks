using CommunityToolkit.Mvvm.ComponentModel;
using GneissBooks.Saft;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        // todo support permanent currencyAmount
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
        private int _taxCodeSelectionIndex = 0;

        public string? TaxCode => TaxCodeItems[TaxCodeSelectionIndex].TaxCode;

        static public List<TaxCodeViewModel> TaxCodeItems { get; } = new() // Todo get from books
        {
            new TaxCodeViewModel("N/A", null),
            new TaxCodeViewModel("Zero rate", TaxCodes.NoTax),
            new TaxCodeViewModel("Incoming (from purchase), high rate", TaxCodes.IncomingPurchaseTaxHighRate),
            new TaxCodeViewModel("Outgoing (from sale), high rate", TaxCodes.OutgoingSaleTaxHighRate),
            new TaxCodeViewModel("Import, high rate, supplier invoice", TaxCodes.ImportHighRate_SupplierInvoice),
            new TaxCodeViewModel("Import, high rate, tax", TaxCodes.ImportHighRate_Tax),
            new TaxCodeViewModel("Export, zero rate", TaxCodes.Export)
        };

        static public ObservableCollection<CustomerViewModel> CustomerItems { get; } = new() // Todo get from books
        {
            new CustomerViewModel("test", "10"),
            new CustomerViewModel("tsest2", "11")
        };

        public decimal AmountNumeric => decimal.TryParse(Amount, out decimal amountNumeric) ? amountNumeric : 0m;

        public TransactionLineViewModel()
        {
        }

        public TransactionLineViewModel(AuditFileGeneralLedgerEntriesJournalTransactionLine rawLine) 
        {
            var amount = (rawLine.Item.CurrencyAmount == null || (rawLine.Item.CurrencyAmount == 0 && rawLine.Item.Amount != 0)) ? rawLine.Item.Amount : rawLine.Item.CurrencyAmount;
            Amount = (rawLine.ItemElementName == ItemChoiceType4.CreditAmount ? amount : -amount).ToString();
            AccountId = rawLine.AccountID;
            SupplierId = rawLine.SupplierID;
            CustomerId = rawLine.CustomerID;
            Description = rawLine.Description;
            Currency = rawLine.Item.CurrencyCode;
            TaxCodeSelectionIndex = TaxCodeItems.FindIndex((item) => { return item.TaxCode == rawLine.TaxInformation?.FirstOrDefault()?.TaxCode; });
        }
    }

    public class TaxCodeViewModel
    {
        public string Description { get; } = "";
        public string? TaxCode { get; }

        public TaxCodeViewModel(string description, string? taxCode) 
        {
            Description = description;
            TaxCode = taxCode;
        }
    }

    public class CustomerViewModel
    {
        public string Name { get; }
        public string CustomerId { get; }

        public CustomerViewModel(string name, string customerId)
        {
            Name = name;
            CustomerId = customerId;
        }

        public override string ToString()
        {
            return $"{CustomerId}: {Name}";
        }
    }
}
