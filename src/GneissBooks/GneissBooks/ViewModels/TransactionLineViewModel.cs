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
        private string _description = "";
        [ObservableProperty]
        private EntityViewModel? _customer;
        [ObservableProperty]
        private EntityViewModel? _supplier;
        [ObservableProperty]
        private string? _currencyExchangeRate;
        [ObservableProperty]
        private TaxClassViewModel? _taxClass;
        [ObservableProperty]
        private decimal? _taxBase;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrencyCode))]
        private CurrencyViewModel? _currency;

        public string? CurrencyCode => Currency?.CurrencyCode;

        public ObservableCollection<TaxClassViewModel> TaxClassList => mainViewModel.TaxClassList;
        public ObservableCollection<EntityViewModel> CustomerList => mainViewModel.CustomerList;
        public ObservableCollection<EntityViewModel> SupplierList => mainViewModel.SupplierList;
        public List<CountryViewModel> Countries => MainViewModel.Countries;
        public List<CurrencyViewModel> CurrencyList => MainViewModel.Currencies;

        public decimal AmountNumeric => decimal.TryParse(Amount, out decimal amountNumeric) ? amountNumeric : 0m;

        MainViewModel mainViewModel;

        public TransactionLineViewModel(MainViewModel mainViewModel)
        {
            this.mainViewModel = mainViewModel;
        }

        public TransactionLineViewModel(AuditFileGeneralLedgerEntriesJournalTransactionLine rawLine, MainViewModel mainViewModel)
        {
            this.mainViewModel = mainViewModel;

            var amount = (rawLine.Item.CurrencyAmount == null || (rawLine.Item.CurrencyAmount == 0 && rawLine.Item.Amount != 0)) ? rawLine.Item.Amount : rawLine.Item.CurrencyAmount;
            Amount = (rawLine.ItemElementName == ItemChoiceType4.CreditAmount ? amount : -amount).ToString();
            AccountId = rawLine.AccountID;
            Supplier = mainViewModel.SupplierList.FirstOrDefault(supplier => { return supplier.SupplierCustomerId == rawLine.SupplierID; });
            Customer = mainViewModel.CustomerList.FirstOrDefault(customer => { return customer.SupplierCustomerId == rawLine.CustomerID; });
            Description = rawLine.Description;
            Currency = CurrencyList.FirstOrDefault((item) => { return item.CurrencyCode == rawLine.Item.CurrencyCode; });
            CurrencyExchangeRate = rawLine.Item.ExchangeRate.ToString();
            TaxClass = TaxClassList.FirstOrDefault((item) => { return item.TaxCode == rawLine.TaxInformation?.FirstOrDefault()?.TaxCode; });
            if (rawLine.TaxInformation?.FirstOrDefault()?.TaxBaseSpecified ?? false)
            {
                var taxBase = (rawLine.TaxInformation?.FirstOrDefault()?.TaxBase);
                if (taxBase != amount)
                    TaxBase = taxBase;
            }
        }
    }

    
}
