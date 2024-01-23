using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GneissBooks.Saft;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace GneissBooks.ViewModels
{
    public partial class TransactionViewModel : ViewModelBase
    {
        public string Title => $"{Date.ToShortDateString()}: {Description} - {TotalAmount.ToString("N2")},-";

        public decimal TotalAmount => Lines.Sum(line => { return Math.Max(0, line.AmountNumeric); });

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Title))]
        private DateTime _date = DateTime.Now;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Title))]
        private string _description = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalAmount))]
        [NotifyPropertyChangedFor(nameof(Title))]
        private ObservableCollection<TransactionLineViewModel> _lines = new();

        public DateTimeOffset? DateAsDateTimeOffset { get { return Date; } set { Date = value?.DateTime ?? DateTime.Now; } }

        public TransactionViewModel()
        {
        }

        public TransactionViewModel(AuditFileGeneralLedgerEntriesJournalTransaction rawTransaction) 
        {
            Date = rawTransaction.TransactionDate;
            Description = rawTransaction.Description;
            foreach (var line in rawTransaction.Line)
            {
                _lines.Add(new TransactionLineViewModel(line));
            }
        }

        [RelayCommand]
        public void AddNewBlankTransactionLine()
        {
            Lines.Add(new());
        }
    }
}
