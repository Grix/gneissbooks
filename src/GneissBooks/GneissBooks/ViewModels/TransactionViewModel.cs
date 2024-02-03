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
        public string Title => $"{Date.ToString("yyyy-MM-dd")}: {Description}, {(Lines.FirstOrDefault()?.AmountNumeric ?? 0).ToString("N2")},-";

        public decimal TotalAmount => Lines.Sum(line => { return Math.Max(0, line.AmountNumeric); });
        public decimal MaxAmount => Lines.Max(line => { return Math.Max(0, line.AmountNumeric); });

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Title))]
        [NotifyPropertyChangedFor(nameof(DateAsDateTimeOffset))]
        private DateTime _date = DateTime.Now;

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

        public EntityViewModel? CustomerSupplier
        {
            get
            {
                foreach (var line in Lines)
                {
                    if (line.Supplier != null)
                        return line.Supplier;
                    else if (line.Customer != null)
                        return line.Customer;
                }
                return null;
            }
        }

        MainViewModel mainViewModel;

        public DateTimeOffset? DateAsDateTimeOffset { get { return Date; } set { Date = value?.DateTime ?? DateTime.Now; } }

        public TransactionViewModel(MainViewModel mainViewModel)
        {
            this.mainViewModel = mainViewModel;
        }

        public TransactionViewModel(AuditFileGeneralLedgerEntriesJournalTransaction rawTransaction, MainViewModel mainViewModel) 
        {
            this.mainViewModel = mainViewModel;

            Date = rawTransaction.TransactionDate;
            Description = rawTransaction.Description;
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
