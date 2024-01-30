using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        private string _amount = "0.00";
        [ObservableProperty]
        private AccountViewModel? _account;
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
        public ObservableCollection<AccountViewModel> AccountList => mainViewModel.AccountList;

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
            Amount = (rawLine.ItemElementName == ItemChoiceType4.DebitAmount ? amount : -amount).ToString("N2");
            Account = AccountList.FirstOrDefault(account => { return account.AccountId == rawLine.AccountID; });
            Supplier = SupplierList.FirstOrDefault(supplier => { return supplier.SupplierCustomerId == rawLine.SupplierID; });
            Customer = CustomerList.FirstOrDefault(customer => { return customer.SupplierCustomerId == rawLine.CustomerID; });
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

        [RelayCommand]
        public void RemoveTransactionLine()
        {
            mainViewModel.NewManualTransaction?.Lines?.Remove(this);
        }
    }

    
}
