using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GneissBooks.Saft;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace GneissBooks.ViewModels;

public partial class PaymentProcessingViewModel : ViewModelBase
{

    [ObservableProperty]
    private ErrorViewModel? _errorViewModel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleEntities))]
    private bool _onlyShowsAccountsWithBalance = true;

    public IEnumerable<AccountViewModel> BankAccounts => mainViewModel.AccountList.Where(account => { return account.AccountId.StartsWith("19") || account.AccountId.StartsWith("20"); });
    public IEnumerable<AccountViewModel> CustomerSupplierAccounts => mainViewModel.AccountList.Where(account => { return account.AccountId.StartsWith("15") || account.AccountId.StartsWith("24"); });

    [ObservableProperty]
    private string _documentPath = "";


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleEntities))]
    private AccountViewModel? _accountFilter;

    public IEnumerable<EntityViewModel> VisibleEntities => allEntities.Where(
        entity =>
        {
            //if (transaction.Date < FilterDate)
            //    return false;
            if (OnlyShowsAccountsWithBalance && (entity.ClosingBalance) == 0m)
                return false;
            //if (AccountFilter != null && entity.AccountId != AccountFilter.AccountId)
            //    return false;
            // todo remake account filtering

            return true;
        }
    );

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleEntities))]
    private DateTimeOffset _filterDate = DateTimeOffset.Now - TimeSpan.FromDays(60);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedAnyEntity))]
    private ObservableCollection<EntityViewModel> _selectedEntities = new();

    [ObservableProperty]
    private AccountViewModel? _paymentAccount = null;

    [ObservableProperty]
    private AccountViewModel? _customerSupplierAccount;

    /*[ObservableProperty]
    private decimal? _settledAmount = null;

    [ObservableProperty]
    private CurrencyViewModel? _settledAmountCurrency = null;*/

    [ObservableProperty]
    private decimal _totalAmount = 0m;

    [ObservableProperty]
    private CurrencyViewModel? _totalAmountCurrency = null;

    [ObservableProperty]
    private decimal _fee = 0m;

    [ObservableProperty]
    private CurrencyViewModel? _feeCurrency = null;

    [ObservableProperty]
    private DateTimeOffset _date = DateTimeOffset.Now;

    public IEnumerable<CurrencyViewModel> Currencies => MainViewModel.Currencies;

    public bool HasSelectedAnyEntity => SelectedEntities.Any(entity => VisibleEntities.Contains(entity));

    private MainViewModel mainViewModel;
    private IEnumerable<EntityViewModel> allEntities => mainViewModel.CustomerList.Concat(mainViewModel.SupplierList);


    public PaymentProcessingViewModel()
    {
        mainViewModel = new MainViewModel();
    }

    public PaymentProcessingViewModel(MainViewModel mainViewModel)
    {
        this.mainViewModel = mainViewModel;
    }

    [RelayCommand]
    public async Task Submit()
    {
        ErrorViewModel = null;

        try
        {
            if (!SelectedEntities.Any())
                throw new Exception("Must choose one or more customers/suppliers that are settled by the payment");
            if (PaymentAccount == null)
                throw new Exception("Must choose account where the payment is settled");
            if (!Path.Exists(DocumentPath))
                throw new Exception("Must choose source document");

            var paymentLines = new List<TransactionLine>();
            string description = "Betaling";
            var sumOfBalancesInNok = 0m;

            foreach (var entity in SelectedEntities)
            {
                if (!VisibleEntities.Contains(entity))
                    continue;

                if (CustomerSupplierAccount?.AccountId == null)
                    throw new Exception("Invalid customer/supplier account");
                if (entity.SupplierCustomerId == null)
                    throw new Exception("Customer/supplier had invalid ID");

                var amountInNok = entity.ClosingBalance;

                if (CustomerSupplierAccount.AccountId.StartsWith("15"))
                {
                    description = "Innbetaling";
                    paymentLines.Add(new(-amountInNok, CustomerSupplierAccount.AccountId, description: description, customerId: entity.SupplierCustomerId, supplierId: null));
                }
                else if (CustomerSupplierAccount.AccountId.StartsWith("24"))
                {
                    paymentLines.Add(new(-amountInNok, CustomerSupplierAccount.AccountId, description: description, customerId: null, supplierId: entity.SupplierCustomerId));
                }

                sumOfBalancesInNok += amountInNok;

            }

            decimal actualTotalAmountInNok = TotalAmount;

            if (TotalAmountCurrency?.CurrencyCode is string totalAmountCurrencyCode && totalAmountCurrencyCode != "NOK")
                actualTotalAmountInNok *= await ExchangeRateApi.GetExchangeRateInNok(totalAmountCurrencyCode, Date);

            if (actualTotalAmountInNok > 0 != sumOfBalancesInNok > 0)
            {
                actualTotalAmountInNok *= -1;
                TotalAmount *= -1;
            }

            var currencyExchangeProfit = actualTotalAmountInNok - sumOfBalancesInNok;

            if (Fee != 0m)
            {
                paymentLines.Add(new(Fee, "7770", description: "Betalingsgebyr", currency: FeeCurrency?.CurrencyCode));

                if ((TotalAmountCurrency?.CurrencyCode ?? "NOK") == (FeeCurrency?.CurrencyCode ?? "NOK"))
                    TotalAmount -= Fee;
                else if ((FeeCurrency?.CurrencyCode ?? "NOK") == "NOK")
                    TotalAmount -= Fee / await ExchangeRateApi.GetExchangeRateInNok(TotalAmountCurrency!.CurrencyCode!, Date);
                else
                    throw new Exception("Not yet supported to have a fee currency that's different from the payment currency and also not NOK");

            }

            paymentLines.Add(new(TotalAmount, PaymentAccount.AccountId, description: description, currency: TotalAmountCurrency?.CurrencyCode));

            if (currencyExchangeProfit != 0m)
            {
                paymentLines.Add(new(-currencyExchangeProfit, currencyExchangeProfit > 0 ? "8060" : "8160", description: "Valutajustering"));
            }

            var documentId = await mainViewModel.Books.AddTransaction(Date, description, paymentLines);
            File.Move(DocumentPath, Path.Combine(Path.GetDirectoryName(DocumentPath)!, documentId + Path.GetExtension(DocumentPath)));

            await mainViewModel.RefreshTransactionList();
            // todo why doesn't this update the list (closingbalance) in the paymentprocessingview window?
            await mainViewModel.RefreshCustomerList();
            await mainViewModel.RefreshSupplierList();

            TotalAmount = 0;
            //SettledAmount = null;
            //SettledAmountCurrency = null;
            FeeCurrency = null;
            TotalAmountCurrency = null;
            Fee = 0;
            SelectedEntities.Clear();
            DocumentPath = "";
            Date = DateTimeOffset.Now.Date;
            AccountFilter = null;
            PaymentAccount = null;
            CustomerSupplierAccount = null;

        }
        catch (Exception ex)
        {
            ErrorViewModel = new(ex);
        }
    }
}
