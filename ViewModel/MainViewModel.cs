using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.Build.Evaluation;
using Microsoft.WindowsAPICodePack.Dialogs;
using ResxCleaner.Exceptions;
using ResxCleaner.Properties;
using ResxCleaner.Services;
using ResxCleaner.View;

namespace ResxCleaner.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private XDocument resourceDocument;
        private HashSet<string> resourceKeys;
        private List<string> files;
        private List<string> extensionList;
        private List<string> referenceFormatList;
        private List<string> excludePrefixesList;

        public MainViewModel()
        {
            this.ProjectFolder = Settings.Default.ProjectFolder;
            this.ResourceFile = Settings.Default.ResourceFile;
            this.Extensions = Settings.Default.Extensions;
            this.ReferenceFormats = Settings.Default.ReferenceFormats;
            this.ExcludePrefixes = Settings.Default.ExcludePrefixes;

            if (string.IsNullOrEmpty(this.Extensions))
            {
                this.Extensions = ".cs,.xaml";
            }

            if (string.IsNullOrEmpty(this.ReferenceFormats))
            {
                this.ReferenceFormats = "AppResources.%";
            }
        }

        public void OnClose()
        {
            Settings.Default.ProjectFolder = this.ProjectFolder;
            Settings.Default.ResourceFile = this.ResourceFile;
            Settings.Default.Extensions = this.Extensions;
            Settings.Default.ReferenceFormats = this.ReferenceFormats;
            Settings.Default.ExcludePrefixes = this.ExcludePrefixes;
        }

        private string projectFolder;
        public string ProjectFolder
        {
            get { return this.projectFolder; }
            set { this.Set(ref this.projectFolder, value); }
        }

        private string resourceFile;
        public string ResourceFile
        {
            get { return this.resourceFile; }
            set { this.Set(ref this.resourceFile, value); }
        }

        private string extensions;
        public string Extensions
        {
            get { return this.extensions; }
            set { this.Set(ref this.extensions, value); }
        }

        private string referenceFormats;
        public string ReferenceFormats
        {
            get { return this.referenceFormats; }
            set { this.Set(ref this.referenceFormats, value); }
        }

        private string excludePrefixes;
        public string ExcludePrefixes
        {
            get { return this.excludePrefixes; }
            set { this.Set(ref this.excludePrefixes, value); }
        }

        private ObservableCollection<StringResource> unusedResources;
        public ObservableCollection<StringResource> UnusedResources
        {
            get { return this.unusedResources; }
            set { this.Set(ref this.unusedResources, value); }
        }

        private string status;
        public string Status
        {
            get { return this.status; }
            set { this.Set(ref this.status, value); }
        }

        private bool working;
        public bool Working
        {
            get { return this.working; }
            set
            {
                this.Set(ref this.working, value);

                DispatchService.BeginInvoke(() =>
                {
                    this.RefreshCommand.RaiseCanExecuteChanged();
                    this.CopyCommand.RaiseCanExecuteChanged();
                    this.DeleteAllCommand.RaiseCanExecuteChanged();
                    this.DeleteSelectedCommand.RaiseCanExecuteChanged();
                    this.ExcludeSelectedCommand.RaiseCanExecuteChanged();
                    this.BrowseFolderCommand.RaiseCanExecuteChanged();
                    this.BrowseResourceFileCommand.RaiseCanExecuteChanged();
                });
            }
        }

        public bool ItemsSelected
        {
            get { return this.UnusedResources != null && this.UnusedResources.Any(r => r.IsSelected); }
        }

        private RelayCommand browseFolderCommand;

        public RelayCommand BrowseFolderCommand
        {
            get
            {
                return this.browseFolderCommand ?? (this.browseFolderCommand = new RelayCommand(
                    () =>
                    {
                        var dialog = new CommonOpenFileDialog
                        {
                            IsFolderPicker = true,
                            EnsurePathExists = true,
                            EnsureValidNames = true,
                            Multiselect = false
                        };

                        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                        {
                            this.ProjectFolder = dialog.FileName;
                        }
                    },
                    () => !this.Working));
            }
        }

        private RelayCommand browseResourceFileCommand;

        public RelayCommand BrowseResourceFileCommand
        {
            get
            {
                return this.browseResourceFileCommand ?? (this.browseResourceFileCommand = new RelayCommand(
                    () =>
                    {
                        var dialog = new CommonOpenFileDialog
                        {
                            IsFolderPicker = false,
                            EnsurePathExists = true,
                            EnsureValidNames = true,
                            Multiselect = false
                        };
                        dialog.Filters.Add(new CommonFileDialogFilter("RESX file", ".resx"));

                        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                        {
                            this.ResourceFile = dialog.FileName;
                        }
                    },
                    () => !this.Working));
            }
        }

        private RelayCommand refreshCommand;

        public RelayCommand RefreshCommand
        {
            get
            {
                return this.refreshCommand ?? (this.refreshCommand = new RelayCommand(
                    () =>
                    {
                        Task.Run(() =>
                        {
                            this.RefreshUnusedList();
                        });
                    },
                    () => !this.Working));
            }
        }

        private RelayCommand copyCommand;

        public RelayCommand CopyCommand
        {
            get
            {
                return this.copyCommand ?? (this.copyCommand = new RelayCommand(
                    () =>
                    {
                        var clipboardText = string.Join(Environment.NewLine, this.UnusedResources.Select(r => r.Key));
                        if (ClipboardService.SetText(clipboardText))
                        {
                            MessageBox.Show("Copied " + this.UnusedResources.Count + " key(s) to the clipboard.");
                        }
                    },
                    () => !this.Working && this.UnusedResources != null && this.UnusedResources.Count > 0));
            }
        }

        private RelayCommand deleteSelectedCommand;

        public RelayCommand DeleteSelectedCommand
        {
            get
            {
                return this.deleteSelectedCommand ?? (this.deleteSelectedCommand = new RelayCommand(
                    () =>
                    {
                        this.Delete(new HashSet<string>(this.UnusedResources.Where(r => r.IsSelected).Select(r => r.Key)));
                    },
                    () => !this.Working));
            }
        }

        private RelayCommand deleteAllCommand;

        public RelayCommand DeleteAllCommand
        {
            get
            {
                return this.deleteAllCommand ?? (this.deleteAllCommand = new RelayCommand(
                    () =>
                    {
                        if (MessageBox.Show("Are you sure you want to delete " + this.UnusedResources.Count + " resources?", "Confirm delete", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                        {
                            Task.Run(() =>
                            {
                                this.Delete(new HashSet<string>(this.resourceKeys));
                            });
                        }
                    },
                    () => !this.Working && this.UnusedResources != null && this.UnusedResources.Count > 0));
            }
        }

        private RelayCommand excludeSelectedCommand;

        public RelayCommand ExcludeSelectedCommand
        {
            get
            {
                return this.excludeSelectedCommand ?? (this.excludeSelectedCommand = new RelayCommand(
                    () =>
                    {
                        var selectedItems = this.UnusedResources.Where(r => r.IsSelected).ToList();
                        foreach (var selectedItem in selectedItems)
                        {
                            this.UnusedResources.Remove(selectedItem);
                            this.resourceKeys.Remove(selectedItem.Key);
                        }

                        this.Status = "Excluded " + selectedItems.Count + " item(s).";

                        this.CopyCommand.RaiseCanExecuteChanged();
                        this.DeleteAllCommand.RaiseCanExecuteChanged();
                    },
                    () => !this.Working));
            }
        }

        private RelayCommand<SelectionChangedEventArgs> onSelectionChangedCommand;

        public RelayCommand<SelectionChangedEventArgs> OnSelectionChangedCommand
        {
            get
            {
                return this.onSelectionChangedCommand ?? (this.onSelectionChangedCommand = new RelayCommand<SelectionChangedEventArgs>(e =>
                {
                    this.RaisePropertyChanged(() => this.ItemsSelected);
                }));
            }
        }

        private void RefreshUnusedList()
        {
            try
            {
                this.Working = true;

                this.PopulateSearchList();
                this.FindUnusedResources();

                this.Working = false;
            }
            catch (FileException exception)
            {
                HandleException(exception);
            }
            catch (ParseException exception)
            {
                HandleException(exception);
            }
        }

        private void PopulateSearchList()
        {
            this.Status = "Populating search list...";

            this.resourceKeys = new HashSet<string>();
            this.excludePrefixesList = this.ExcludePrefixes?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList() ?? new List<string>();

            this.resourceDocument = XDocument.Load(this.ResourceFile);

            ForEveryDataElement(this.resourceDocument, dataElement =>
            {
                var name = GetName(dataElement);

                var addName = this.excludePrefixesList.All(excludedPrefix => !name.StartsWith(excludedPrefix));

                if (addName)
                {
                    this.resourceKeys.Add(name);
                }
            });
        }

        private void FindUnusedResources()
        {
            this.PopulateFileList();

            this.Status = "Finding unused keys...";

            this.referenceFormatList = this.ReferenceFormats.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            foreach (var file in this.files)
            {
                this.RemoveFoundKeys(file);
            }

            // Remaining keys are unused
            var newUnusedResources = new ObservableCollection<StringResource>();
            foreach (var unusedKey in this.resourceKeys)
            {
                newUnusedResources.Add(new StringResource { Key = unusedKey });
            }

            this.UnusedResources = newUnusedResources;

            this.Status = "Getting resource values...";

            this.PopulateUnusedResourceValues();

            this.Status = "Found " + this.UnusedResources.Count + " unused resource(s).";
        }

        private void PopulateUnusedResourceValues()
        {
            ForEveryDataElement(this.resourceDocument, dataElement =>
            {
                var unusedResource = this.UnusedResources.FirstOrDefault(r => r.Key == GetName(dataElement));
                if (unusedResource != null)
                {
                    var valueElement = dataElement.Element("value");
                    if (valueElement != null)
                    {
                        unusedResource.Value = valueElement.Value;
                    }
                }
            });
        }

        private void PopulateFileList()
        {
            this.Status = "Populating file list...";

            this.extensionList = this.Extensions.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(e => CleanExtension(e.Trim())).ToList();
            this.files = new List<string>();

            try
            {
                this.PopulateFileList(this.ProjectFolder);
            }
            catch (IOException exception)
            {
                throw new FileException("Could not populate file list.", exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new FileException("Could not populate file list.", exception);
            }
        }

        private static string CleanExtension(string extension)
        {
            if (extension.StartsWith("."))
            {
                return extension;
            }

            return "." + extension;
        }

        private void PopulateFileList(string directoryPath)
        {
            foreach (var file in Directory.GetFiles(directoryPath).Where(file => this.extensionList.Any(file.EndsWith)))
            {
                this.files.Add(file);
            }

            foreach (var subDirectory in Directory.GetDirectories(directoryPath))
            {
                this.PopulateFileList(subDirectory);
            }
        }

        // Finds instances of resourceKeys in the given file and removes them from the collection if they are found
        private void RemoveFoundKeys(string filePath)
        {
            var fileText = File.ReadAllText(filePath);
            var foundResources = this.resourceKeys.Where(key => this.referenceFormatList.Select(referenceFormat => referenceFormat.Replace("%", key)).Any(searchString => fileText.Contains(searchString))).ToList();

            foreach (var key in foundResources)
            {
                this.resourceKeys.Remove(key);
            }
        }

        private void Delete(HashSet<string> stringsToDelete)
        {
            try
            {
                this.Working = true;

                var count = stringsToDelete.Count;
                this.Status = "Deleting " + count + " unused resource(s)...";

                RemoveFromResx(stringsToDelete);
                RemoveFromProject(stringsToDelete);
                RemoveFromResourceFolder(stringsToDelete);
                RemoveFromResourceKeys(stringsToDelete);
                RemoveFromUnusedResources(stringsToDelete);

                this.Status = "Deleted " + count + " unused resource(s).";
                this.Working = false;
            }
            catch (FileException exception)
            { 
                HandleException(exception);
            }
            catch (ParseException exception)
            {
                HandleException(exception);
            }
        }

        /// <summary>
        /// Remove from unused resources
        /// </summary>
        /// <param name="stringsToDelete"></param>
        public void RemoveFromUnusedResources(HashSet<string> stringsToDelete)
        {
            DispatchService.BeginInvoke(() =>
            {
                for (var i = this.UnusedResources.Count - 1; i >= 0; i--)
                {
                    if (stringsToDelete.Contains(this.UnusedResources[i].Key))
                    {
                        this.UnusedResources.RemoveAt(i);
                    }
                }
            });
        }

        /// <summary>
        /// Remove from the collections
        /// </summary>
        /// <param name="stringsToDelete"></param>
        public void RemoveFromResourceKeys(HashSet<string> stringsToDelete)
        {
            foreach (var deletedKey in stringsToDelete)
            {
                this.resourceKeys.Remove(deletedKey);
            }
        }

        /// <summary>
        /// Remove From Resouce File
        /// </summary>
        /// <param name="stringsToDelete"></param>
        public void RemoveFromResx(HashSet<string> stringsToDelete)
        {
            var document = XDocument.Load(this.ResourceFile);
            ForEveryDataElement(document, dataElement =>
            {
                if (stringsToDelete.Contains(GetName(dataElement)))
                {
                    dataElement.Remove();
                }
            });

            try
            {
                document.Save(this.ResourceFile);
            }
            catch (IOException ex)
            {
                throw new FileException("Error saving resource file.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new FileException("Error saving resource file.", ex);
            }
            catch (XmlException ex)
            {
                throw new FileException("Error saving resource file.", ex);
            }
        }

        /// <summary>
        /// Remove From Project
        /// </summary>
        /// <param name="stringsToDelete"></param>
        private void RemoveFromProject(HashSet<string> stringsToDelete)
        {
            var projectFile = Directory.GetFiles(ProjectFolder, "*.csproj").FirstOrDefault();
            if (projectFile == null)
            {
                throw new ParseException("No project file found.");
            }

            var project = new Project(projectFile);
            var noneItemGroups = project.GetItems("None").ToList();
            for (var i = noneItemGroups.Count - 1; i >= 0; i--)
            {

                var item = noneItemGroups.ElementAt(i);
                if (UnusedResources.Where(a => stringsToDelete.Contains(a.Key))
                                   .Select(ur => ur.Value.ToString())
                                   .Any(a => a.Contains(item.EvaluatedInclude)))
                {
                   project.RemoveItem(item);
                }
            }

            try
            {
               project.Save();
            }
            catch (IOException ex)
            {
                throw new FileException("Error saving project file.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new FileException("Error saving project file.", ex);
            }
            catch (XmlException ex)
            {
                throw new FileException("Error saving project file.", ex);
            }
        }

        /// <summary>
        /// Remove From Resource Folder
        /// </summary>
        /// <param name="stringsToDelete"></param>
        private void RemoveFromResourceFolder(HashSet<string> stringsToDelete)
        {
            var resourceFolder = Directory.GetDirectories(this.ProjectFolder)
                .FirstOrDefault(s => Path.GetFileName((s ?? string.Empty).TrimEnd('\\')) == "Resources");
            if (resourceFolder == null)
            {
                throw new ParseException("No resource folder found.");
            }

            foreach (var resource in this.UnusedResources.Where(a => stringsToDelete.Contains(a.Key)))
            {
                var resourceValue = resource.Value;
                var fileName = Path.GetFileName(resourceValue.Substring(0, resourceValue.IndexOf(";", StringComparison.Ordinal)));
                var filePath = Path.Combine(resourceFolder, fileName);

                try
                {
                    File.Delete(filePath);
                }
                catch (IOException ex)
                {
                    throw new FileException("Error deleting resource file.", ex);
                }
                catch (UnauthorizedAccessException exception)
                {
                    throw new FileException("Error deleting resource file.", exception);
                }
            }
        }

        private static void ForEveryDataElement(XDocument document, Action<XElement> action)
        {
            if (document.Root == null)
            {
                throw new ParseException("No root element found.");
            }

            foreach (var dataElement in document.Root.Elements("data").ToList())
            {
                action(dataElement);
            }
        }

        private static string GetName(XElement dataElement)
        {
            var nameAttribute = dataElement.Attribute("name");
            if (nameAttribute == null)
            {
                throw new ParseException("Name attribute missing on data.");
            }

            return nameAttribute.Value;
        }

        private static void HandleException(Exception exception)
        {
            DispatchService.BeginInvoke(() =>
            {
                MessageBox.Show(exception.Message);
            });
        }
    }
}
