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

namespace GneissBooks.ViewModels
{
    public partial class PaymentProcessingViewModel : ViewModelBase
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(VisibleTransactions))]
        private bool _onlyShowsAccountsWithBalance = true;

        public IEnumerable<AccountViewModel> Accounts => mainViewModel.AccountList.Where(account => { return account.AccountId.StartsWith("15") || account.AccountId.StartsWith("24"); });

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(VisibleTransactions))]
        private AccountViewModel? _accountFilter;

        public IEnumerable<TransactionViewModel> VisibleTransactions => mainViewModel.TransactionList.Where(
            transaction =>
            {
                var customerSupplier = transaction.CustomerSupplier;
                if (OnlyShowsAccountsWithBalance && (customerSupplier?.CurrentAmountNumeric ?? 0m) == 0m)
                    return false;
                if (AccountFilter != null && customerSupplier?.AccountId != AccountFilter.AccountId)
                    return false;

                return true;
            }
        );

        [ObservableProperty]
        private ObservableCollection<TransactionViewModel> _selectedTransactions = new();

        private MainViewModel mainViewModel;

        public PaymentProcessingViewModel()
        {
            mainViewModel = new MainViewModel();
        }

        public PaymentProcessingViewModel(MainViewModel mainViewModel)
        {
            this.mainViewModel = mainViewModel;
        }
    }
}
