﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using com.PorcupineSupernova.RootCauseTreeCore;
using Microsoft.Win32;

namespace com.PorcupineSupernova.RootCauseTreeWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MainWindowViewModel vm;
        private OpenFileDialog openFileDlg = new OpenFileDialog();
        private SaveFileDialog newFileDlg = new SaveFileDialog();
        private string rootCauseFilter = "Root Cause Files|*.rootcause";
        private string rootCauseExt = ".rootcause";
        private double scaleFactor = 1.2;
        private bool leftMouseDragged = false;
        private Point lastMousePos;
        private double MaxScale = 3;
        private double MinScale = 0.3;
        private Node contextMenuNode;
        private Graphing.RootCauseEdge contextMenuEdge;
        private Graphing.RootCauseVertex linkParent;
        private bool isLinking;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ExitApp_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            bool? didOpen = openFileDlg.ShowDialog();
            if (didOpen.HasValue && didOpen.Value == true)
            {
                OpenFilePath(openFileDlg.FileName);
            }
            openFileDlg.FileName = string.Empty;
        }

        private void OpenFilePath(string path)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var info = new System.IO.FileInfo(path);
            try
            {
                vm.OpenFile(path);
            }
            catch (InvalidRootCauseFileException)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show("The selected file does not appear to be a valid root cause tree.  Please select a different file.", "Invalid File");
            }
            catch (RootCauseFileLockedException)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show("The selected file is either marked as read-only or is in use by another user or application and cannot be opened at this time.", "File Cannot Be Opened");
            }
            catch (Exception)
            {
                Mouse.OverrideCursor = null;
                throw;
            }
            Mouse.OverrideCursor = null;
        }

    private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            vm = DataContext as MainWindowViewModel;

            openFileDlg.Multiselect = false;
            openFileDlg.DefaultExt = rootCauseExt;
            openFileDlg.Filter = rootCauseFilter;
            SetUpSaveDialog(newFileDlg, rootCauseExt, rootCauseFilter);
            vm.CanInteractWithMenuArea = true;
            RootCauseLayout.LayoutParameters = vm.algs;
            vm.FileChanged += FileChanged;
            PopulateVersion();

            if (App.Current.Properties["StartupFile"] != null)
            {
                string file = App.Current.Properties["StartupFile"].ToString();
                OpenFilePath(file);
            }
        }

        private void PopulateVersion()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            var versionArray = info.FileVersion.Split('.').Take(3);
            VersionLabel.Header = $"Version {string.Join(".",versionArray)}";
        }

        private void SetUpSaveDialog(SaveFileDialog dlg,string extension,string filter)
        {
            dlg.CheckPathExists = true;
            dlg.DefaultExt = extension;
            dlg.Filter = filter;
            dlg.AddExtension = true;
        }

        private void TreeContainer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var position = e.GetPosition(TreeCanvas);
            var transform = TreeCanvas.RenderTransform as MatrixTransform;
            var matrix = transform.Matrix;

            var scale = e.Delta >= 0 ? scaleFactor : (1.0 / scaleFactor);
            var newScaleFactor = matrix.M11 * scale;
            if (!(newScaleFactor < MaxScale && newScaleFactor > MinScale)) return;

            matrix.ScaleAtPrepend(scale, scale, position.X, position.Y);
            transform.Matrix = matrix;
            TreeCanvas.UpdateLayout();
        }

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            bool? didSelectFile = newFileDlg.ShowDialog();
            Mouse.OverrideCursor = Cursors.Wait;
            if (didSelectFile.HasValue && didSelectFile.Value == true)
            {
                vm.NewFile(newFileDlg.FileName);
            }
            Mouse.OverrideCursor = null;
            newFileDlg.FileName = string.Empty;
        }

        private void AddProblem_Click(object sender, RoutedEventArgs e)
        {
            var textDlg = new TextDialog(this,"Add Problem", "Enter a clear and concise problem statement, focusing on describing the symptoms while minimizing assumptions about possible causes.");
            if (textDlg.ShowDialog().Value)
            {
                vm.CreateProblem(textDlg.Text);
            }
        }

        private void TreeContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            leftMouseDragged = true;
            lastMousePos = e.GetPosition(this);
        }

        private void TreeContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            leftMouseDragged = false;
        }

        private void TreeContainer_MouseMove(object sender, MouseEventArgs e)
        {
            if (!leftMouseDragged) return;

            var position = e.GetPosition(this);
            var transform = TreeCanvas.RenderTransform as MatrixTransform;
            var matrix = transform.Matrix;
            matrix.Translate(position.X - lastMousePos.X, position.Y - lastMousePos.Y);
            transform.Matrix = matrix;
            lastMousePos = position;
            TreeCanvas.UpdateLayout();
        }

        private void ProblemList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TreeCanvas.Margin = new Thickness(ProblemList.ActualWidth + 50, 50, 0, 0);
            TreeCanvas.RenderTransform = new MatrixTransform();
        }

        private void Node_Click(object sender,RoutedEventArgs e)
        {
            contextMenuNode = (((FrameworkElement)sender).DataContext as Graphing.RootCauseVertex).Source;
            if (isLinking)
            {
                CreateLink(contextMenuNode);
            }
            else
            {
                var textDlg = new TextDialog(this, "Add Cause", $"{contextMenuNode.Text}\n", "\uE019 Because             Therefore \uE018");
                if (textDlg.ShowDialog().Value)
                {
                    vm.CreateChildNode(textDlg.Text, contextMenuNode);
                }
            }
        }

        private void OpenNodeMenu_Click(object sender,RoutedEventArgs e)
        {
            contextMenuNode = (((FrameworkElement)sender).DataContext as Graphing.RootCauseVertex).Source;
            if (isLinking)
            {
                CreateLink(contextMenuNode);
            }
            else
            {
                ContextMenu menu;
                if (contextMenuNode is Problem)
                {
                    menu = FindResource("ProblemMenu") as ContextMenu;
                }
                else
                {
                    menu = FindResource("CauseMenu") as ContextMenu;
                }
                menu.PlacementTarget = sender as Button;
                menu.IsOpen = true;
            }
        }

        private void Edge_Click(object sender, MouseButtonEventArgs e)
        {
            contextMenuEdge = ((FrameworkElement)sender).DataContext as Graphing.RootCauseEdge;
            var menu = FindResource("EdgeMenu") as ContextMenu;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        private void StartLink_Click(object sender, RoutedEventArgs e)
        {
            linkParent = ((FrameworkElement)sender).DataContext as Graphing.RootCauseVertex;
            linkParent.Select();
            isLinking = true;
            vm.CanInteractWithMenuArea = false;
        }

        private void CancelLink_Click(object sender, RoutedEventArgs e)
        {
            linkParent.UnSelect();
            linkParent = null;
            isLinking = false;
            vm.CanInteractWithMenuArea = true;
        }

        private void EditNodeText_Click(object sender, RoutedEventArgs e)
        {
            var textDlg = new TextDialog(this, "Edit Text", $"Old text:\n{contextMenuNode.Text}\n\nNew text:");
            textDlg.NodeText.Text = contextMenuNode.Text;
            if (textDlg.ShowDialog().Value)
            {
                vm.EditNodeText(textDlg.Text, contextMenuNode);
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            vm.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            vm.Redo();
        }

        private void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            var exportWindow = new ExportGraph(vm.CurrentProblem.InitialProblem);
            exportWindow.Owner = this;
            exportWindow.ShowDialog();
            return;
        }

        private void DeleteProblem_Click(object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show("Are you sure you want to delete the entire root cause tree?  This action cannot be undone.", "Caution", MessageBoxButton.YesNo);
            if (answer == MessageBoxResult.Yes)
            {
                vm.DeleteProblem();
            }
        }

        private void DeleteCauseChain_Click(object sender, RoutedEventArgs e)
        {
            vm.DeleteCauseChain(contextMenuNode);
        }

        private void DeleteCause_Click(object sender, RoutedEventArgs e)
        {
            vm.DeleteCause(contextMenuNode);
        }

        private void RemoveLink_Click(object sender, RoutedEventArgs e)
        {
            vm.RemoveLink(contextMenuEdge.Source.Source, contextMenuEdge.Target.Source);
        }

        private void CreateLink(Node child)
        {
            vm.AddLink(linkParent.Source, child);
            isLinking = false;
            linkParent = null;
            vm.CanInteractWithMenuArea = true;
        }

        private bool RootCauseLayoutClick;

        private void RootCauseLayout_LeftMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (RootCauseLayoutClick && RootCauseLayout.HighlightedEdges.Count() > 0)
            {
                contextMenuEdge = RootCauseLayout.HighlightedEdges.First();
                var menu = FindResource("EdgeMenu") as ContextMenu;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                menu.IsOpen = true;
            }
            RootCauseLayoutClick = false;
        }

        private void RootCauseLayout_LeftMouseDown(object sender, MouseButtonEventArgs e)
        {
            RootCauseLayoutClick = true;
        }

        private void RootCauseLayout_MouseMove(object sender, MouseEventArgs e)
        {
            RootCauseLayoutClick = false;
        }

        private void SetTitle(string file)
        {
            Title = $"Arborist: {file}";
        }

        private void CloseFile_Click(object sender, RoutedEventArgs e)
        {
            vm.CloseFile();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SqliteDb.GetInstance().CloseConnection();
        }

        private void FileChanged(string fileName)
        {
            SetTitle(fileName);
        }
    }
}
