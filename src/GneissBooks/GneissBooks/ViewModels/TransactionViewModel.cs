using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GneissBooks.Saft;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace GneissBooks.ViewModels
{
    public partial class TransactionViewModel : ViewModelBase
    {
        public string Title => $"{TransactionId ?? ""}, {Date: yyyy-MM-dd}: {Description}, {Math.Abs(Lines.FirstOrDefault()?.Amount ?? 0).ToString("N2")} {Lines.FirstOrDefault()?.CurrencyCode ?? "NOK"}";

        public decimal TotalAmount => Lines.Sum(line => { return Math.Max(0, line.Amount ?? 0m); });
        public decimal MaxAmount => Lines.Max(line => { return Math.Max(0, line.Amount ?? 0m); });

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Title))]
        public string? _transactionId;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Title))]
        private DateTimeOffset _date = DateTimeOffset.Now;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Title))]
        private string _description = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalAmount))]
        [NotifyPropertyChangedFor(nameof(Title))]
        private ObservableCollection<TransactionLineViewModel> _lines = new();

        [ObservableProperty]
        private string _documentPath = "";

        [ObservableProperty]
        private TransactionLineViewModel? _selectedTransactionLine;

        public IEnumerable<EntityViewModel> CustomersAndSuppliers
        {
            get
            {
                foreach (var line in Lines)
                {
                    if (line.Supplier != null)
                        yield return line.Supplier;
                    else if (line.Customer != null)
                        yield return line.Customer;
                }
            }
        }

        public IEnumerable<AccountViewModel> Accounts
        {
            get
            {
                foreach (var line in Lines)
                {
                    if (line.Account != null) 
                        yield return line.Account;
                }
            }
        }

        MainViewModel mainViewModel;

        public TransactionViewModel(MainViewModel mainViewModel)
        {
            this.mainViewModel = mainViewModel;
        }

        public TransactionViewModel(AuditFileGeneralLedgerEntriesJournalTransaction rawTransaction, MainViewModel mainViewModel) 
        {
            this.mainViewModel = mainViewModel;

            Date = rawTransaction.TransactionDate;
            Description = rawTransaction.Description;
            TransactionId = rawTransaction.TransactionID;
            foreach (var line in rawTransaction.Line)
            {
                var lineViewModel = new TransactionLineViewModel(line, mainViewModel);
                _lines.Add(lineViewModel);
            }
        }

        [RelayCommand]
        public void AddNewBlankTransactionLine()
        {
            Lines.Add(new(mainViewModel));
        }

        public bool GetHasTransactionLineFromAccount(AccountViewModel account)
        {
            foreach (var line in Lines)
                if (line.Account == account)
                    return true;

            return false;
        }

        public override string ToString() => Title;
    }
}
