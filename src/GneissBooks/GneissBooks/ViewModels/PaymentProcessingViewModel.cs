using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GneissBooks.Saft;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace GneissBooks.ViewModels;

public partial class PaymentProcessingViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleTransactions))]
    private bool _onlyShowsAccountsWithBalance = true;

    public IEnumerable<AccountViewModel> BankAccounts => mainViewModel.AccountList.Where(account => { return account.AccountId.StartsWith("19") || account.AccountId.StartsWith("20"); });
    public IEnumerable<AccountViewModel> CustomerSupplierAccounts => mainViewModel.AccountList.Where(account => { return account.AccountId.StartsWith("15") || account.AccountId.StartsWith("24"); });

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleTransactions))]
    private AccountViewModel? _accountFilter;

    public IEnumerable<TransactionViewModel> VisibleTransactions => transactionList.Where(
        transaction =>
        {
            var customerSupplier = transaction.CustomerSupplier;
            if (transaction.Date < FilterDate)
                return false;
            if (OnlyShowsAccountsWithBalance && (customerSupplier?.ClosingBalance ?? 0m) == 0m)
                return false;
            if (AccountFilter != null && !transaction.GetHasTransactionLineFromAccount(AccountFilter))
                return false;

            return true;
        }
    );

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleTransactions))]
    private DateTimeOffset _filterDate = DateTimeOffset.Now - TimeSpan.FromDays(60);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedAnyTransaction))]
    private ObservableCollection<TransactionViewModel> _selectedTransactions = new();

    [ObservableProperty]
    private AccountViewModel? _paymentAccount = null;

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

    public bool HasSelectedAnyTransaction => SelectedTransactions.Any(transaction => VisibleTransactions.Contains(transaction));


    private MainViewModel mainViewModel;
    private IEnumerable<TransactionViewModel> transactionList;// => mainViewModel.TransactionList;


    public PaymentProcessingViewModel()
    {
        mainViewModel = new MainViewModel();
        transactionList = mainViewModel.TransactionList.Where(transaction => {
            foreach (var line in transaction.Lines)
            {
                if (line.Supplier != null)
                {
                    if (line.Amount < 0)
                        return true;
                }
                else if (line.Customer != null)
                {
                    if (line.Amount > 0)
                        return true;
                }
            }
            return false;
        });
    }

    public PaymentProcessingViewModel(MainViewModel mainViewModel)
    {
        this.mainViewModel = mainViewModel;
        transactionList = mainViewModel.TransactionList.Where(transaction => {
            foreach (var line in transaction.Lines)
            {
                if (line.Supplier != null)
                {
                    if (line.Amount < 0)
                        return true;
                }
                else if (line.Customer != null)
                {
                    if (line.Amount > 0)
                        return true;
                }
            }
            return false;
        });
    }

    [RelayCommand]
    public async Task Submit()
    {
        if (PaymentAccount == null)
            throw new Exception("Must choose account where the payment is settled");

        var paymentLines = new List<TransactionLine>();

        foreach (var transaction in SelectedTransactions)
        {
            if (!VisibleTransactions.Contains(transaction))
                continue;

            var sumOfPaymentsInNok = 0m;

            foreach (var line in transaction.Lines)
            {
                if ((line.Account?.AccountId?.StartsWith("15") ?? false) || (line.Account?.AccountId?.StartsWith("24") ?? false))
                {
                    var currencyCode = line.Currency?.CurrencyCode;

                    // todo store amount in NOK in TransactionLineViewModel to avoid this mess
                    decimal amountInNok = line.Amount ?? 0m;

                    if (currencyCode != null && currencyCode != "NOK")
                    {
                        if (line.CurrencyExchangeRate is decimal exchangeRate)
                            amountInNok *= exchangeRate;
                        else
                            amountInNok *= await ExchangeRateApi.GetExchangeRateInNok(currencyCode, transaction.Date);
                    }

                    sumOfPaymentsInNok += amountInNok;

                    paymentLines.Add(new(-amountInNok, line.Account.AccountId, description: "Payment",
                                        customerId: line.Customer?.SupplierCustomerId, supplierId: line.Supplier?.SupplierCustomerId));

                    break;
                }
            }

            decimal actualTotalAmountInNok = TotalAmount;
            if (TotalAmountCurrency?.CurrencyCode is string totalAmountCurrencyCode && totalAmountCurrencyCode != "NOK")
                actualTotalAmountInNok *= await ExchangeRateApi.GetExchangeRateInNok(totalAmountCurrencyCode, Date);

            if (actualTotalAmountInNok > 0 != sumOfPaymentsInNok > 0)
                actualTotalAmountInNok *= -1;

            var currencyExchangeProfit = actualTotalAmountInNok - sumOfPaymentsInNok;

            if (Fee != 0m)
            {
                paymentLines.Add(new(Fee, "7770", description: "Payment fees", currency: FeeCurrency?.CurrencyCode));

                if ((TotalAmountCurrency?.CurrencyCode ?? "NOK") == (FeeCurrency?.CurrencyCode ?? "NOK"))
                    TotalAmount -= Fee;
                else if ((FeeCurrency?.CurrencyCode ?? "NOK") == "NOK")
                    TotalAmount -= Fee / await ExchangeRateApi.GetExchangeRateInNok(TotalAmountCurrency!.CurrencyCode!, Date);
                else
                    throw new Exception("Not yet supported to have a fee currency that's different from the payment currency and also not NOK");

            }

            paymentLines.Add(new(TotalAmount, PaymentAccount.AccountId, description: "Payment", currency: TotalAmountCurrency?.CurrencyCode));

            if (currencyExchangeProfit != 0m)
            {
                paymentLines.Add(new(-currencyExchangeProfit, currencyExchangeProfit > 0 ? "8060" : "8160", description: "Currency fluctuation"));
            }

            var documentId = mainViewModel.Books.AddTransaction(Date.DateTime, "Payment", paymentLines);

            await mainViewModel.RefreshTransactionList(); 
            // todo why doesn't this update the list (closingbalance) in the paymentprocessingview window?
            await mainViewModel.RefreshCustomerList();
            await mainViewModel.RefreshSupplierList();
            // todo use documentId, rename file
        }
    }
}
