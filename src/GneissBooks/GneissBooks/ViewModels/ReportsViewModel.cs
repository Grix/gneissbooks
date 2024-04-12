using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GneissBooks.Saft;
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

public partial class ReportsViewModel : ViewModelBase
{
    [ObservableProperty]
    private DateTimeOffset _periodStart = DateTimeOffset.Now - TimeSpan.FromDays(30);
    [ObservableProperty]
    private DateTimeOffset _periodEnd = DateTimeOffset.Now;
    [ObservableProperty]
    private AccountViewModel? _selectedAccount;

    [ObservableProperty]
    private decimal _totalRevenue;
    [ObservableProperty]
    private decimal _totalExpenses;
    [ObservableProperty]
    private decimal _operationalRevenue;
    [ObservableProperty]
    private decimal _operationalExpenses;
    [ObservableProperty]
    private decimal _grossProfit;
    [ObservableProperty]
    private decimal _changeInAssetsAllAccounts;
    [ObservableProperty]
    private decimal _totalAssetBalanceAtEndAllAccounts;
    [ObservableProperty]
    private decimal _outgoingVat;
    [ObservableProperty]
    private decimal _incomingVat;
    [ObservableProperty]
    private decimal _changeInBalanceSelectedAccount;
    [ObservableProperty]
    private decimal _totalBalanceAtEndSelectedAccount;

    public IEnumerable<AccountViewModel> AccountList => mainViewModel.AccountList;

    private MainViewModel mainViewModel;


    public ReportsViewModel()
    {
        mainViewModel = new MainViewModel();
        PeriodStart = new DateTimeOffset(PeriodStart.Year, PeriodStart.Month, 1, 0, 0, 0, PeriodStart.Offset);

        Update();
    }

    public ReportsViewModel(MainViewModel mainViewModel)
    {
        this.mainViewModel = mainViewModel;
        PeriodStart = new DateTimeOffset(PeriodStart.Year, PeriodStart.Month, 1, 0, 0, 0, PeriodStart.Offset);

        Update();
    }

    partial void OnPeriodEndChanged(DateTimeOffset value)
    {
        Update();
    }

    partial void OnPeriodStartChanged(DateTimeOffset value)
    {
        Update();
    }

    partial void OnSelectedAccountChanged(AccountViewModel? value)
    {
        Update();
    }

    [RelayCommand]
    public async Task Update()
    {
        decimal operationalExpenses = 0;
        decimal operationalRevenue = 0;
        decimal totalExpenses = 0;
        decimal totalRevenue = 0;
        decimal changeInAssets = 0;
        decimal totalAssetBalanceAtEnd = 0;
        decimal outgoingVat = 0;
        decimal incomingVat = 0;
        decimal changeInBalanceSelectedAccount = 0;
        decimal totalBalanceAtEndSelectedAccount = SelectedAccount?.OpeningBalance ?? 0;


        foreach (var transaction in mainViewModel.Books.Transactions)
        {
            var isInPeriod = transaction.TransactionDate.Date >= PeriodStart.Date && transaction.TransactionDate.Date <= PeriodEnd.Date;

            // Todo count total asset balance
            foreach (var line in transaction.Line)
            {
                /*if ((line.AccountID.StartsWith("1") || line.AccountID.StartsWith("2")) && !line.AccountID.StartsWith("206"))
                {
                    decimal amount = line.ItemElementName == Saft.ItemChoiceType4.DebitAmount ? line.Item.Amount : -line.Item.Amount;
                    OperationalRevenue -= amount;
                    TotalRevenue -= amount;
                }*/
                if (line.AccountID == SelectedAccount?.AccountId)
                {
                    decimal amount = line.ItemElementName == Saft.ItemChoiceType4.DebitAmount ? line.Item.Amount : -line.Item.Amount;
                    totalBalanceAtEndSelectedAccount += amount; // todo don't count after end of period
                    if (isInPeriod)
                        changeInBalanceSelectedAccount += amount;
                }
            }

            if (!isInPeriod)
                continue;

            foreach (var line in transaction.Line)
            {
                if (line.AccountID.StartsWith("3"))
                {
                    decimal amount = line.ItemElementName == Saft.ItemChoiceType4.DebitAmount ? line.Item.Amount : -line.Item.Amount;
                    operationalRevenue -= amount;
                    totalRevenue -= amount;
                }
                else if (line.AccountID.StartsWith("4") || line.AccountID.StartsWith("5") || line.AccountID.StartsWith("6") || line.AccountID.StartsWith("7"))
                {
                    decimal amount = line.ItemElementName == Saft.ItemChoiceType4.DebitAmount ? line.Item.Amount : -line.Item.Amount;
                    operationalExpenses += amount;
                    totalExpenses += amount;
                }
                else if (line.AccountID.StartsWith("80"))
                {
                    decimal amount = line.ItemElementName == Saft.ItemChoiceType4.DebitAmount ? line.Item.Amount : -line.Item.Amount;
                    totalRevenue -= amount;
                }
                else if (line.AccountID.StartsWith("81"))
                {
                    decimal amount = line.ItemElementName == Saft.ItemChoiceType4.DebitAmount ? line.Item.Amount : -line.Item.Amount;
                    totalExpenses += amount;
                }
                else if (line.AccountID.StartsWith("270"))
                {
                    decimal amount = line.ItemElementName == Saft.ItemChoiceType4.DebitAmount ? line.Item.Amount : -line.Item.Amount;
                    outgoingVat -= amount;
                }
                else if (line.AccountID.StartsWith("271"))
                {
                    decimal amount = line.ItemElementName == Saft.ItemChoiceType4.DebitAmount ? line.Item.Amount : -line.Item.Amount;
                    incomingVat += amount;
                }
            }
        }

        OperationalExpenses = operationalExpenses;
        TotalExpenses = totalExpenses;
        OperationalRevenue = operationalRevenue;
        TotalRevenue = totalRevenue;
        GrossProfit = TotalRevenue - TotalExpenses;
        IncomingVat = incomingVat;
        OutgoingVat = outgoingVat;
        TotalBalanceAtEndSelectedAccount = totalBalanceAtEndSelectedAccount;
        ChangeInBalanceSelectedAccount = changeInBalanceSelectedAccount;
    }

}
