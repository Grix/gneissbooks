using Avalonia.Metadata;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GneissBooks.ViewModels;

public partial class ErrorViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text))]
    private Exception _exception;

    public string Text => Exception.Message;

    public ErrorViewModel(Exception exception)
    {
        Exception = exception;
    }

}
