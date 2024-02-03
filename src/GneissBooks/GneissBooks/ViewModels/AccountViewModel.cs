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
    public partial class AccountViewModel : ViewModelBase
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StandardAccountId))]
        private string _accountId = "0000";

        [ObservableProperty]
        private string _description = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(OpeningBalanceNumeric))]
        private string _openingBalance = "0";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ClosingBalanceNumeric))]
        private string _closingBalance = "0";

        public decimal OpeningBalanceNumeric => decimal.TryParse(OpeningBalance, out decimal amountNumeric) ? amountNumeric : 0m;
        public decimal ClosingBalanceNumeric => decimal.TryParse(ClosingBalance, out decimal amountNumeric) ? amountNumeric : 0m;
        public string? StandardAccountId => (AccountId.Length >= 2) ? AccountId.Substring(0, 2) : null;

        public AccountViewModel()
        {

        }

        public AccountViewModel(AuditFileMasterFilesAccount rawAccount) 
        {
            AccountId = rawAccount.AccountID;
            Description = rawAccount.AccountDescription;
            OpeningBalance = (rawAccount.ItemElementName == ItemChoiceType.OpeningDebitBalance ? rawAccount.Item : -rawAccount.Item).ToString();
            ClosingBalance = (rawAccount.Item1ElementName == Item1ChoiceType.ClosingDebitBalance ? rawAccount.Item1 : -rawAccount.Item1).ToString();
        }

        public override string ToString()
        {
            return $"{AccountId}: {Description}";
        }
    }
}
