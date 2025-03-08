using System.Windows.Forms;

namespace DualPaneExplorer;

public class MainForm : Form
{
    private readonly SplitContainer splitContainer;
    private readonly ExplorerPane leftPane;
    private readonly ExplorerPane rightPane;

    public MainForm()
    {
        Text = "Dual Pane File Explorer";
        Size = new Size(1200, 800);

        // Initialize split container
        splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = Width / 2
        };

        // Initialize panes
        leftPane = new ExplorerPane();
        rightPane = new ExplorerPane();

        // Add panes to split container
        splitContainer.Panel1.Controls.Add(leftPane);
        splitContainer.Panel2.Controls.Add(rightPane);

        // Add split container to form
        Controls.Add(splitContainer);

        // Set up drag and drop
        AllowDrop = true;
    }
}

public class ExplorerPane : Panel
{
    private readonly TextBox pathBox;
    private readonly Button upButton;
    private readonly ListView fileListView;
    private string currentPath;

    public ExplorerPane()
    {
        Dock = DockStyle.Fill;
        currentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Navigation panel at the top
        var navigationPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30
        };

        // Up button
        upButton = new Button
        {
            Text = "↑",
            Width = 30,
            Height = 23,
            Location = new Point(5, 3)
        };
        upButton.Click += UpButton_Click;

        // Path textbox
        pathBox = new TextBox
        {
            Location = new Point(40, 3),
            Height = 23,
            Width = Width - 45,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };
        pathBox.KeyDown += PathBox_KeyDown;

        navigationPanel.Controls.AddRange(new Control[] { upButton, pathBox });

        // File list view
        fileListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            AllowDrop = true,
            MultiSelect = true
        };

        fileListView.Columns.AddRange(new[]
        {
            new ColumnHeader { Text = "Name", Width = 200 },
            new ColumnHeader { Text = "Size", Width = 100 },
            new ColumnHeader { Text = "Type", Width = 100 },
            new ColumnHeader { Text = "Modified", Width = 150 }
        });

        fileListView.ItemDrag += FileListView_ItemDrag;
        fileListView.DragEnter += FileListView_DragEnter;
        fileListView.DragDrop += FileListView_DragDrop;
        fileListView.DoubleClick += FileListView_DoubleClick;

        Controls.AddRange(new Control[] { navigationPanel, fileListView });

        // Initial directory load
        NavigateToPath(currentPath);
    }

    private void UpButton_Click(object? sender, EventArgs e)
    {
        var parent = Directory.GetParent(currentPath);
        if (parent != null)
        {
            NavigateToPath(parent.FullName);
        }
    }

    private void PathBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            NavigateToPath(pathBox.Text);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void FileListView_DoubleClick(object? sender, EventArgs e)
    {
        if (fileListView.SelectedItems.Count == 1)
        {
            var selectedItem = fileListView.SelectedItems[0];
            var fullPath = Path.Combine(currentPath, selectedItem.Text);

            if (Directory.Exists(fullPath))
            {
                NavigateToPath(fullPath);
            }
        }
    }

    private void FileListView_ItemDrag(object? sender, ItemDragEventArgs e)
    {
        var items = fileListView.SelectedItems.Cast<ListViewItem>()
            .Select(item => Path.Combine(currentPath, item.Text))
            .ToArray();

        if (items.Length > 0)
        {
            var dragData = new DataObject(DataFormats.FileDrop, items);
            fileListView.DoDragDrop(dragData, DragDropEffects.Copy | DragDropEffects.Move);
        }
    }

    private void FileListView_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = (e.KeyState & 8) != 0 ? DragDropEffects.Copy : DragDropEffects.Move;
        }
    }

    private void FileListView_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            var isCopy = (e.KeyState & 8) != 0; // Check if Ctrl key is pressed

            foreach (var file in files)
            {
                try
                {
                    var fileName = Path.GetFileName(file);
                    var destPath = Path.Combine(currentPath, fileName);

                    if (isCopy)
                    {
                        if (File.Exists(file))
                            File.Copy(file, destPath, true);
                        else if (Directory.Exists(file))
                            CopyDirectory(file, destPath);
                    }
                    else
                    {
                        if (File.Exists(file))
                            File.Move(file, destPath);
                        else if (Directory.Exists(file))
                            Directory.Move(file, destPath);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error processing {file}: {ex.Message}", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            RefreshView();
        }
    }

    private void NavigateToPath(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                currentPath = path;
                pathBox.Text = path;
                RefreshView();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error accessing path: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshView()
    {
        fileListView.Items.Clear();

        try
        {
            // Add directories
            foreach (var dir in Directory.GetDirectories(currentPath))
            {
                var dirInfo = new DirectoryInfo(dir);
                var item = new ListViewItem(dirInfo.Name);
                item.SubItems.AddRange(new[]
                {
                    "<DIR>",
                    "Folder",
                    dirInfo.LastWriteTime.ToString()
                });
                fileListView.Items.Add(item);
            }

            // Add files
            foreach (var file in Directory.GetFiles(currentPath))
            {
                var fileInfo = new FileInfo(file);
                var item = new ListViewItem(fileInfo.Name);
                item.SubItems.AddRange(new[]
                {
                    FormatFileSize(fileInfo.Length),
                    fileInfo.Extension,
                    fileInfo.LastWriteTime.ToString()
                });
                fileListView.Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error refreshing view: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    private void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        var dirs = dir.GetDirectories();

        Directory.CreateDirectory(destinationDir);

        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        foreach (var subDir in dirs)
        {
            var newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }
}
