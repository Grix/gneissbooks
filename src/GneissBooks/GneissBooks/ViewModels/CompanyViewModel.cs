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
    public partial class CompanyViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _description = "";

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
