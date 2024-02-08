using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using GneissBooks.ViewModels;
using System.Linq;

namespace GneissBooks;

public partial class PaymentProcessingView : UserControl
{
    public PaymentProcessingView()
    {
        InitializeComponent();
    }

    public async void DocumentPathBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose transaction document",
            AllowMultiple = false,
            FileTypeFilter = new FilePickerFileType[] {
                new("PDF files") { Patterns = new[] { "*.pdf" } },
                new("All files") { Patterns = new[] { "*" } }
            }
        });

        if (files is not null && files.Count == 1)
        {
            (DataContext as PaymentProcessingViewModel)!.DocumentPath = files.First().Path.LocalPath;
        }
    }
}