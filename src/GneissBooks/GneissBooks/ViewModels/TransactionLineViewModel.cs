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
        [NotifyPropertyChangedFor(nameof(AmountTooltip))]
        [NotifyPropertyChangedFor(nameof(AmountNumeric))]
        private string _amount = "0.00";
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AmountTooltip))]
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

        public string AmountTooltip
        {
            get
            {
                if (Account?.StandardAccountId is not string standardAccountId)
                    return "";

                if (standardAccountId == "14")
                    return "Positive for replenishing stock, negative for using stock";
                if (standardAccountId == "15")
                    return "Positive for sales invoice, negative for payment";
                if (standardAccountId == "20" || standardAccountId == "19")
                    return "Positive for account balance increasing, negative for account balance decreasing";
                if (standardAccountId == "24")
                    return "Positive for payment, negative for purchase invoice";
                if (standardAccountId.StartsWith("3"))
                    return "Positive for reversals, negative for normal income";
                if (standardAccountId.StartsWith("4") || standardAccountId.StartsWith("6") || standardAccountId.StartsWith("7"))
                    return "Positive for normal expenses, negative for reversals or offsets such as private use of internet";
                if (standardAccountId == "80")
                    return "Negative for currency profits";
                if (standardAccountId == "81")
                    return "Positive for currency losses";
                if (standardAccountId == "27")
                    return "Positive for taxes on purchases (2710), negative for taxes on sales (2700). On settlement (2740): Negative if you owe VAT, positive if you are owed VAT.";
                return "";
            }
        }

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
            Account = AccountList.FirstOrDefault(account => { return account.AccountId == rawLine.AccountID; }); // Todo handle if unknown account is specified
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
