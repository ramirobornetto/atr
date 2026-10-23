using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace AchievementTimelineReplay
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                MessageBox.Show(
                    e.Exception.ToString(),
                    "Achievement Timeline Replay - Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };

            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Exception ex = e.ExceptionObject as Exception;
                MessageBox.Show(
                    ex != null ? ex.ToString() : Convert.ToString(e.ExceptionObject),
                    "Achievement Timeline Replay - Error fatal",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };

            Application.Run(new MainForm());
        }
    }

    internal enum SamCliMode
    {
        Unknown,
        Modern,
        Legacy
    }

    internal sealed class AchievementRecord
    {
        public string ApiId;
        public string DisplayName;
        public string Description;
        public long OriginalUnix;
        public bool Included = true;
        public double? ManualGapSeconds;
        public string SteamState = "—";

        public AchievementRecord Clone()
        {
            AchievementRecord a = new AchievementRecord();
            a.ApiId = ApiId;
            a.DisplayName = DisplayName;
            a.Description = Description;
            a.OriginalUnix = OriginalUnix;
            a.Included = Included;
            a.ManualGapSeconds = ManualGapSeconds;
            a.SteamState = SteamState;
            return a;
        }
    }

    internal sealed class GameEntry
    {
        public uint AppId;
        public uint AccountId;
        public string GameName;
        public string AppType;
        public bool IsOwned;
        public bool HasLocalHistory;
        public bool HasSnapshot;
        public string StatsPath;
        public string SchemaPath;
        public List<AchievementRecord> Achievements = new List<AchievementRecord>();

        public override string ToString()
        {
            string history = HasLocalHistory
                ? String.Format("{0} logros históricos", Achievements.Count)
                : "sin historial local";
            return String.Format("{0} [{1}] · {2}", GameName, AppId, history);
        }
    }

    internal sealed class SnapshotData
    {
        public int Version { get; set; }
        public uint AppId { get; set; }
        public uint AccountId { get; set; }
        public string GameName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public List<SnapshotAchievement> Achievements { get; set; }
    }

    internal sealed class SnapshotAchievement
    {
        public string ApiId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public long OriginalUnix { get; set; }
    }

    internal sealed class ScheduleItem
    {
        public AchievementRecord Achievement;
        public double GapSeconds;
        public double OffsetSeconds;
        public DateTime Due;
    }

    internal sealed class ProcessResult
    {
        public int ExitCode;
        public string StdOut;
        public string StdErr;
        public bool TimedOut;
    }

    internal sealed class MainForm : Form
    {
        private TextBox txtSteam;
        private TextBox txtSam;
        private Label lblSamMode;

        private Button btnScan;
        private ProgressBar scanProgress;
        private Label lblScan;

        private ComboBox cmbGames;
        private Button btnOpenSam;
        private Button btnQuerySteam;
        private Label lblSnapshot;

        private DataGridView grid;
        private SplitContainer workspaceSplit;
        private Point dragStartPoint;
        private int dragRowIndex = -1;
        private bool liveDragging;
        private AchievementRecord liveDragRecord;
        private Button btnUp;
        private Button btnDown;
        private Button btnRestore;
        private Button btnClearManual;
        private CheckBox chkCustomOrder;

        private NumericUpDown nudHours;
        private NumericUpDown nudMinutes;
        private NumericUpDown nudSeconds;
        private NumericUpDown nudInitialDelay;
        private CheckBox chkLockFirst;
        private CheckBox chkIdleDuringMigration;
        private Label lblOriginalDuration;
        private Label lblActualDuration;
        private Label lblSelected;

        private Button btnStart;
        private Button btnCancel;
        private ProgressBar progress;
        private Label lblStatus;
        private TextBox txtLog;
        private NotifyIcon trayIcon;

        private readonly object logLock = new object();
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();

        private List<GameEntry> allGames = new List<GameEntry>();
        private List<AchievementRecord> currentOrder = new List<AchievementRecord>();
        private GameEntry currentGame;

        private SamCliMode samMode = SamCliMode.Unknown;
        private bool updatingGrid;
        private volatile bool running;
        private volatile bool cancelRequested;
        private volatile bool scanning;
        private volatile bool cancelScanRequested;
        private Thread workerThread;
        private Thread scanThread;
        private string scanSteamPath;
        private string scanSamPath;
        private SamCliMode scanSamMode;

        private string activeSamPath;
        private SamCliMode activeSamMode;
        private uint activeAppId;
        private string activeGameName;
        private bool activeLockFirst;
        private bool activeIdleDuringMigration;
        private int activeInitialDelaySeconds;

        public MainForm()
        {
            Text = "Achievement Timeline Replay v0.5.3";
            Width = 1380;
            Height = 860;
            MinimumSize = new Size(1080, 700);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);

            json.MaxJsonLength = Int32.MaxValue;

            BuildUi();
            LoadSettings();
            AutoDetect();

            Shown += delegate
            {
                try
                {
                    int total = workspaceSplit.ClientSize.Height;

                    if (total > 360)
                    {
                        workspaceSplit.Panel1MinSize = 200;
                        workspaceSplit.Panel2MinSize = 95;

                        int desired = (int)(total * 0.72);
                        int maxAllowed = total - workspaceSplit.Panel2MinSize - workspaceSplit.SplitterWidth;

                        workspaceSplit.SplitterDistance = Math.Max(
                            workspaceSplit.Panel1MinSize,
                            Math.Min(desired, maxAllowed));
                    }
                }
                catch (Exception ex)
                {
                    AppendLog("Aviso al ajustar el divisor de la interfaz: " + ex.Message);
                }
            };

            lblStatus.Text = "Listo. Presioná “Escanear biblioteca” para comenzar.";
        }

        private void BuildUi()
        {
            TableLayoutPanel main = new TableLayoutPanel();
            main.Dock = DockStyle.Fill;
            main.ColumnCount = 1;
            main.RowCount = 6;
            main.Padding = new Padding(10);
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(main);

            Label intro = new Label();
            intro.AutoSize = true;
            intro.Padding = new Padding(0, 0, 0, 8);
            intro.Text = "Migra historiales locales de logros a tu cuenta de Steam usando SamRewritten. " +
                         "La app conserva un snapshot del historial original, resuelve el nombre público de Steam por AppID y reproduce sus tiempos con la escala que elijas.";
            main.Controls.Add(intro, 0, 0);

            TableLayoutPanel paths = new TableLayoutPanel();
            paths.Dock = DockStyle.Top;
            paths.AutoSize = true;
            paths.ColumnCount = 4;
            paths.RowCount = 2;
            paths.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            paths.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            paths.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            paths.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            paths.Controls.Add(new Label { Text = "Steam:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            txtSteam = new TextBox { Dock = DockStyle.Fill };
            paths.Controls.Add(txtSteam, 1, 0);
            Button btnSteamBrowse = new Button { Text = "Elegir…", AutoSize = true };
            btnSteamBrowse.Click += delegate { BrowseSteam(); };
            paths.Controls.Add(btnSteamBrowse, 2, 0);
            Button btnDetect = new Button { Text = "Detectar", AutoSize = true };
            btnDetect.Click += delegate { AutoDetectSteam(); };
            paths.Controls.Add(btnDetect, 3, 0);

            paths.Controls.Add(new Label { Text = "SamRewritten CLI:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            txtSam = new TextBox { Dock = DockStyle.Fill };
            paths.Controls.Add(txtSam, 1, 1);
            Button btnSamBrowse = new Button { Text = "Elegir…", AutoSize = true };
            btnSamBrowse.Click += delegate { BrowseSam(); };
            paths.Controls.Add(btnSamBrowse, 2, 1);
            Button btnSamCheck = new Button { Text = "Validar", AutoSize = true };
            btnSamCheck.Click += delegate { ValidateSam(true); };
            paths.Controls.Add(btnSamCheck, 3, 1);
            main.Controls.Add(paths, 0, 1);

            FlowLayoutPanel scanBar = new FlowLayoutPanel();
            scanBar.Dock = DockStyle.Fill;
            scanBar.AutoSize = true;
            scanBar.WrapContents = false;
            scanBar.Padding = new Padding(0, 6, 0, 4);

            btnScan = new Button { Text = "Escanear biblioteca", AutoSize = true };
            btnScan.Click += delegate
            {
                if (scanning)
                    cancelScanRequested = true;
                else
                    StartScan();
            };
            scanBar.Controls.Add(btnScan);

            scanProgress = new ProgressBar();
            scanProgress.Width = 260;
            scanProgress.Height = 22;
            scanProgress.Margin = new Padding(12, 3, 8, 0);
            scanBar.Controls.Add(scanProgress);

            lblScan = new Label { Text = "Sin escanear.", AutoSize = true, Margin = new Padding(0, 6, 12, 0) };
            scanBar.Controls.Add(lblScan);


            main.Controls.Add(scanBar, 0, 2);

            FlowLayoutPanel gameBar = new FlowLayoutPanel();
            gameBar.Dock = DockStyle.Fill;
            gameBar.AutoSize = true;
            gameBar.WrapContents = false;
            gameBar.Padding = new Padding(0, 4, 0, 6);
            gameBar.Controls.Add(new Label { Text = "Juego:", AutoSize = true, Margin = new Padding(0, 7, 6, 0) });

            cmbGames = new ComboBox();
            cmbGames.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbGames.Width = 600;
            cmbGames.SelectedIndexChanged += delegate { OnGameChanged(); };
            gameBar.Controls.Add(cmbGames);

            btnOpenSam = new Button { Text = "Abrir en SAM", AutoSize = true, Enabled = false };
            btnOpenSam.Click += delegate { OpenSelectedInSam(); };
            gameBar.Controls.Add(btnOpenSam);

            btnQuerySteam = new Button { Text = "Consultar estado Steam", AutoSize = true, Enabled = false };
            btnQuerySteam.Click += delegate { QuerySteamState(); };
            gameBar.Controls.Add(btnQuerySteam);

            lblSnapshot = new Label { Text = "Snapshot: —", AutoSize = true, Margin = new Padding(12, 7, 0, 0) };
            gameBar.Controls.Add(lblSnapshot);

            lblSamMode = new Label { Text = "SAM: sin validar", AutoSize = true, Margin = new Padding(14, 7, 0, 0) };
            gameBar.Controls.Add(lblSamMode);

            main.Controls.Add(gameBar, 0, 3);

            grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.BackgroundColor = SystemColors.Window;
            grid.CurrentCellDirtyStateChanged += delegate
            {
                if (grid.IsCurrentCellDirty)
                    grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.CellValueChanged += GridCellValueChanged;
            grid.CellEndEdit += GridCellEndEdit;
            grid.AllowDrop = true;
            grid.MouseDown += GridMouseDown;
            grid.MouseMove += GridMouseMove;
            grid.MouseUp += GridMouseUp;
            grid.DragOver += GridDragOver;
            grid.DragDrop += GridDragDrop;

            DataGridViewCheckBoxColumn cInc = new DataGridViewCheckBoxColumn();
            cInc.Name = "include";
            cInc.HeaderText = "✓";
            cInc.Width = 36;
            grid.Columns.Add(cInc);

            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "order", HeaderText = "#", Width = 42, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Logro", Width = 250, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "description", HeaderText = "Descripción", Width = 380, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "original", HeaderText = "Desbloqueo original", Width = 145, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "originalGap", HeaderText = "Espera original", Width = 110, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "newGap", HeaderText = "Espera nueva", Width = 105, ReadOnly = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "targetOffset", HeaderText = "Momento nuevo", Width = 110, ReadOnly = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "targetTime", HeaderText = "Hora estimada", Width = 120, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "steam", HeaderText = "Steam", Width = 125, ReadOnly = true });

            workspaceSplit = new SplitContainer();
            workspaceSplit.Dock = DockStyle.Fill;
            workspaceSplit.Orientation = Orientation.Horizontal;
            workspaceSplit.SplitterWidth = 7;
            workspaceSplit.Panel1.Controls.Add(grid);

            TableLayoutPanel bottomArea = new TableLayoutPanel();
            bottomArea.Dock = DockStyle.Fill;
            bottomArea.ColumnCount = 1;
            bottomArea.RowCount = 2;
            bottomArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bottomArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            workspaceSplit.Panel2.Controls.Add(bottomArea);

            main.Controls.Add(workspaceSplit, 0, 4);

            TableLayoutPanel opts = new TableLayoutPanel();
            opts.Dock = DockStyle.Fill;
            opts.AutoSize = true;
            opts.ColumnCount = 2;
            opts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            opts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));

            FlowLayoutPanel orderPanel = new FlowLayoutPanel();
            orderPanel.AutoSize = true;
            orderPanel.Dock = DockStyle.Fill;

            chkCustomOrder = new CheckBox { Text = "Orden personalizado", AutoSize = true };
            chkCustomOrder.CheckedChanged += delegate
            {
                btnUp.Enabled = chkCustomOrder.Checked && currentGame != null && currentGame.HasLocalHistory && !running;
                btnDown.Enabled = btnUp.Enabled;
                if (!chkCustomOrder.Checked && currentGame != null && currentGame.HasLocalHistory)
                    RestoreOriginalOrder();
            };
            orderPanel.Controls.Add(chkCustomOrder);

            btnUp = new Button { Text = "↑ Subir", AutoSize = true, Enabled = false };
            btnDown = new Button { Text = "↓ Bajar", AutoSize = true, Enabled = false };
            btnRestore = new Button { Text = "Restaurar orden original", AutoSize = true, Enabled = false };
            btnClearManual = new Button { Text = "Limpiar tiempos manuales", AutoSize = true, Enabled = false };

            btnUp.Click += delegate { MoveSelected(-1); };
            btnDown.Click += delegate { MoveSelected(1); };
            btnRestore.Click += delegate { RestoreOriginalOrder(); };
            btnClearManual.Click += delegate { ClearManualTimings(); };

            orderPanel.Controls.Add(btnUp);
            orderPanel.Controls.Add(btnDown);
            orderPanel.Controls.Add(btnRestore);
            orderPanel.Controls.Add(btnClearManual);
            opts.Controls.Add(orderPanel, 0, 0);

            FlowLayoutPanel durationPanel = new FlowLayoutPanel();
            durationPanel.AutoSize = true;
            durationPanel.Dock = DockStyle.Fill;
            durationPanel.Controls.Add(new Label { Text = "Duración total automática:", AutoSize = true, Margin = new Padding(0, 7, 4, 0) });

            nudHours = MakeNumeric(0, 9999, 0, 58);
            nudMinutes = MakeNumeric(0, 59, 0, 48);
            nudSeconds = MakeNumeric(0, 59, 0, 48);

            durationPanel.Controls.Add(nudHours);
            durationPanel.Controls.Add(new Label { Text = "h", AutoSize = true, Margin = new Padding(0, 7, 4, 0) });
            durationPanel.Controls.Add(nudMinutes);
            durationPanel.Controls.Add(new Label { Text = "m", AutoSize = true, Margin = new Padding(0, 7, 4, 0) });
            durationPanel.Controls.Add(nudSeconds);
            durationPanel.Controls.Add(new Label { Text = "s", AutoSize = true, Margin = new Padding(0, 7, 0, 0) });

            nudHours.ValueChanged += delegate { RecalculatePlan(); };
            nudMinutes.ValueChanged += delegate { RecalculatePlan(); };
            nudSeconds.ValueChanged += delegate { RecalculatePlan(); };
            opts.Controls.Add(durationPanel, 1, 0);

            FlowLayoutPanel infoPanel = new FlowLayoutPanel();
            infoPanel.AutoSize = true;
            infoPanel.Dock = DockStyle.Fill;
            lblOriginalDuration = new Label { Text = "Historial: —", AutoSize = true, Margin = new Padding(0, 7, 16, 0) };
            lblActualDuration = new Label { Text = "Reproducción: —", AutoSize = true, Margin = new Padding(0, 7, 16, 0) };
            lblSelected = new Label { Text = "Seleccionados: —", AutoSize = true, Margin = new Padding(0, 7, 0, 0) };
            infoPanel.Controls.Add(lblOriginalDuration);
            infoPanel.Controls.Add(lblActualDuration);
            infoPanel.Controls.Add(lblSelected);
            opts.Controls.Add(infoPanel, 0, 1);

            FlowLayoutPanel startOpts = new FlowLayoutPanel();
            startOpts.AutoSize = true;
            startOpts.Dock = DockStyle.Fill;

            chkLockFirst = new CheckBox
            {
                Text = "Modo reparación: bloquear seleccionados antes",
                Checked = false,
                AutoSize = true,
                Margin = new Padding(0, 6, 12, 0)
            };
            startOpts.Controls.Add(chkLockFirst);

            chkIdleDuringMigration = new CheckBox
            {
                Text = "Registrar tiempo de juego durante la migración (SAM idle)",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(0, 6, 12, 0)
            };
            startOpts.Controls.Add(chkIdleDuringMigration);

            startOpts.Controls.Add(new Label { Text = "Espera inicial:", AutoSize = true, Margin = new Padding(0, 7, 4, 0) });
            nudInitialDelay = MakeNumeric(0, 3600, 10, 56);
            nudInitialDelay.ValueChanged += delegate { RecalculatePlan(); };
            startOpts.Controls.Add(nudInitialDelay);
            startOpts.Controls.Add(new Label { Text = "s", AutoSize = true, Margin = new Padding(0, 7, 0, 0) });

            opts.Controls.Add(startOpts, 1, 1);

            bottomArea.Controls.Add(opts, 0, 0);

            FlowLayoutPanel actionBar = new FlowLayoutPanel();
            actionBar.Dock = DockStyle.Fill;
            actionBar.AutoSize = true;
            actionBar.Padding = new Padding(0, 6, 0, 6);

            btnStart = new Button { Text = "▶ Iniciar migración", AutoSize = true, Height = 32, Enabled = false };
            btnCancel = new Button { Text = "Cancelar", AutoSize = true, Height = 32, Enabled = false };
            btnStart.Click += delegate { StartReplay(); };
            btnCancel.Click += delegate { CancelReplay(); };

            actionBar.Controls.Add(btnStart);
            actionBar.Controls.Add(btnCancel);

            progress = new ProgressBar { Width = 260, Height = 22, Margin = new Padding(16, 5, 8, 0) };
            actionBar.Controls.Add(progress);

            lblStatus = new Label { Text = "Listo.", AutoSize = true, Margin = new Padding(4, 8, 0, 0) };
            actionBar.Controls.Add(lblStatus);

            main.Controls.Add(actionBar, 0, 5);

            txtLog = new TextBox();
            txtLog.Dock = DockStyle.Fill;
            txtLog.Multiline = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.ReadOnly = true;
            txtLog.Font = new Font("Consolas", 8.5F);
            bottomArea.Controls.Add(txtLog, 0, 1);

            trayIcon = new NotifyIcon();
            trayIcon.Icon = SystemIcons.Application;
            trayIcon.Text = "Achievement Timeline Replay";
            trayIcon.DoubleClick += delegate { RestoreFromTray(); };
            ContextMenu trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("Mostrar", delegate { RestoreFromTray(); });
            trayMenu.MenuItems.Add("Cancelar reproducción", delegate { CancelReplay(); });
            trayMenu.MenuItems.Add("-");
            trayMenu.MenuItems.Add("Salir", delegate { ExitFromTray(); });
            trayIcon.ContextMenu = trayMenu;

            Resize += delegate
            {
                if (WindowState == FormWindowState.Minimized && running)
                    HideToTray();
            };
            FormClosing += MainFormClosing;
        }

        private NumericUpDown MakeNumeric(decimal min, decimal max, decimal val, int width)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = min;
            n.Maximum = max;
            n.Value = val;
            n.Width = width;
            return n;
        }

        private void AutoDetect()
        {
            if (String.IsNullOrWhiteSpace(txtSteam.Text))
                AutoDetectSteam();

            if (String.IsNullOrWhiteSpace(txtSam.Text))
            {
                string sam = DetectSamPath();
                if (!String.IsNullOrEmpty(sam))
                    txtSam.Text = sam;
            }

            if (!String.IsNullOrWhiteSpace(txtSam.Text))
                ValidateSam(false);
        }

        private void AutoDetectSteam()
        {
            string path = DetectSteamPath();
            if (!String.IsNullOrEmpty(path))
                txtSteam.Text = path;
            SaveSettings();
        }

        private string DetectSteamPath()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("SteamPath");
                        if (value != null && Directory.Exists(value.ToString()))
                            return value.ToString().Replace('/', '\\');
                    }
                }
            }
            catch { }

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("InstallPath");
                        if (value != null && Directory.Exists(value.ToString()))
                            return value.ToString();
                    }
                }
            }
            catch { }

            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string fallback = Path.Combine(pf86, "Steam");
            if (Directory.Exists(fallback))
                return fallback;
            return "";
        }

        private string DetectSamPath()
        {
            List<string> dirs = new List<string>();
            dirs.Add(AppDomain.CurrentDomain.BaseDirectory);

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            dirs.Add(Path.Combine(local, "Programs", "SamRewritten"));
            dirs.Add(Path.Combine(local, "SamRewritten"));
            dirs.Add(Path.Combine(pf, "SamRewritten"));
            dirs.Add(Path.Combine(pf86, "SamRewritten"));

            string[] names = new string[] { "samrewritten-cli.exe", "SamRewritten-cli.exe", "samrewritten.exe", "SamRewritten.exe" };

            foreach (string dir in dirs)
            {
                if (String.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                    continue;

                foreach (string name in names)
                {
                    string direct = Path.Combine(dir, name);
                    if (File.Exists(direct))
                        return direct;
                }

                try
                {
                    foreach (string name in names)
                    {
                        string[] found = Directory.GetFiles(dir, name, SearchOption.AllDirectories);
                        if (found.Length > 0)
                            return found[0];
                    }
                }
                catch { }
            }

            return "";
        }

        private void BrowseSteam()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Seleccioná la carpeta raíz de Steam";
                if (Directory.Exists(txtSteam.Text))
                    dlg.SelectedPath = txtSteam.Text;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    txtSteam.Text = dlg.SelectedPath;
                    SaveSettings();
                }
            }
        }

        private void BrowseSam()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "SamRewritten CLI (*.exe)|*.exe|Todos los archivos (*.*)|*.*";
                dlg.Title = "Seleccioná samrewritten-cli.exe";

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    txtSam.Text = dlg.FileName;
                    SaveSettings();
                    ValidateSam(true);
                }
            }
        }

        private bool ValidateSam(bool showMessage)
        {
            samMode = SamCliMode.Unknown;
            string path = txtSam.Text.Trim();

            if (!File.Exists(path))
            {
                lblSamMode.Text = "SAM: no encontrado";
                if (showMessage)
                    MessageBox.Show(this, "No se encontró el ejecutable seleccionado.", "SamRewritten", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            ProcessResult help = RunProcess(path, "--help", 15000);
            string all = (help.StdOut + "\n" + help.StdErr).ToLowerInvariant();

            if (all.Contains("list-achievements") && all.Contains("list-apps") && all.Contains("unlock"))
                samMode = SamCliMode.Modern;
            else if (all.Contains("--unlock") && all.Contains("--lock"))
                samMode = SamCliMode.Legacy;

            if (samMode == SamCliMode.Modern)
                lblSamMode.Text = "SAM: CLI moderno";
            else if (samMode == SamCliMode.Legacy)
                lblSamMode.Text = "SAM: CLI legacy";
            else
                lblSamMode.Text = "SAM: formato no reconocido";

            if (showMessage)
            {
                if (samMode == SamCliMode.Unknown)
                    MessageBox.Show(this, "El ejecutable responde, pero no pude reconocer su interfaz CLI.", "SamRewritten", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else
                    MessageBox.Show(this, "SamRewritten validado correctamente (" + samMode.ToString() + ").", "SamRewritten", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            SaveSettings();
            return samMode != SamCliMode.Unknown;
        }

        private void StartScan()
        {
            if (scanning || running)
                return;

            string steam = txtSteam.Text.Trim();
            string statsDir = Path.Combine(steam, "appcache", "stats");

            if (!Directory.Exists(statsDir))
            {
                MessageBox.Show(this, "No encontré Steam\\appcache\\stats.", "Escaneo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!ValidateSam(false))
            {
                MessageBox.Show(this, "Seleccioná y validá SamRewritten antes de escanear.", "Escaneo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            scanSteamPath = txtSteam.Text.Trim();
            scanSamPath = txtSam.Text.Trim();
            scanSamMode = samMode;

            scanning = true;
            cancelScanRequested = false;
            btnScan.Text = "Cancelar escaneo";
            btnScan.Enabled = true;
            cmbGames.Enabled = false;
            btnStart.Enabled = false;
            btnOpenSam.Enabled = false;
            btnQuerySteam.Enabled = false;

            scanProgress.Style = ProgressBarStyle.Marquee;
            scanProgress.MarqueeAnimationSpeed = 25;
            lblScan.Text = "Leyendo biblioteca de Steam con SAM…";
            lblStatus.Text = "Escaneando…";

            scanThread = new Thread(ScanWorker);
            scanThread.IsBackground = true;
            scanThread.Start();
        }

        private void ScanWorker()
        {
            try
            {
                // SAM is still useful for the owned-library list and for names it already knows,
                // but ownership no longer decides which local histories are shown.
                Dictionary<uint, GameEntry> libraryByApp = new Dictionary<uint, GameEntry>();
                Dictionary<uint, string> samNames = new Dictionary<uint, string>();

                if (scanSamMode == SamCliMode.Modern)
                {
                    UpdateScanMarquee("Leyendo biblioteca y nombres conocidos con SAM…");

                    ProcessResult apps = RunProcess(scanSamPath, "list-apps", 180000);

                    if (!apps.TimedOut && apps.ExitCode == 0)
                    {
                        foreach (Dictionary<string, object> d in ParseJsonObjectArray(apps.StdOut))
                        {
                            uint appId = GetJsonUInt(d, "app_id", 0);
                            if (appId == 0)
                                continue;

                            string type = GetJsonString(d, "app_type", "App");
                            if (String.Equals(type, "Junk", StringComparison.OrdinalIgnoreCase))
                                continue;

                            string publicName = RepairMojibake(GetJsonString(d, "app_name", ""));

                            GameEntry g = new GameEntry();
                            g.AppId = appId;
                            g.GameName = !String.IsNullOrWhiteSpace(publicName)
                                ? publicName
                                : "AppID " + appId.ToString(CultureInfo.InvariantCulture);
                            g.AppType = type;
                            g.IsOwned = true;
                            g.HasLocalHistory = false;

                            libraryByApp[appId] = g;

                            if (!String.IsNullOrWhiteSpace(publicName))
                                samNames[appId] = publicName;
                        }
                    }
                    else
                    {
                        AppendLog("SAM no pudo enumerar la biblioteca; continúo con los historiales locales.");
                    }
                }

                if (cancelScanRequested)
                    return;

                string steam = scanSteamPath;
                string statsDir = Path.Combine(steam, "appcache", "stats");
                uint recentAccount = GetMostRecentAccountId(steam);

                List<string> statFiles = Directory
                    .GetFiles(statsDir, "UserGameStats_*_*.bin", SearchOption.TopDirectoryOnly)
                    .ToList();

                if (recentAccount != 0)
                {
                    List<string> filtered = statFiles.Where(delegate(string f)
                    {
                        uint account, app;
                        return TryParseStatsFilename(f, out account, out app) && account == recentAccount;
                    }).ToList();

                    if (filtered.Count > 0)
                        statFiles = filtered;
                }

                BeginInvoke((MethodInvoker)delegate
                {
                    scanProgress.Style = ProgressBarStyle.Continuous;
                    scanProgress.MarqueeAnimationSpeed = 0;
                    scanProgress.Minimum = 0;
                    scanProgress.Maximum = Math.Max(1, statFiles.Count);
                    scanProgress.Value = 0;
                    lblScan.Text = "Leyendo historiales locales…";
                });

                Dictionary<uint, GameEntry> rawLocalByApp = new Dictionary<uint, GameEntry>();

                for (int i = 0; i < statFiles.Count; i++)
                {
                    if (cancelScanRequested)
                        return;

                    string file = statFiles[i];
                    uint accountId, appId;

                    if (!TryParseStatsFilename(file, out accountId, out appId))
                        continue;

                    string schema = Path.Combine(
                        statsDir,
                        "UserGameStatsSchema_" + appId.ToString(CultureInfo.InvariantCulture) + ".bin");

                    int progressIndex = i + 1;
                    BeginInvoke((MethodInvoker)delegate
                    {
                        scanProgress.Value = Math.Min(scanProgress.Maximum, progressIndex);
                        lblScan.Text = String.Format(
                            "Historiales: {0}/{1} · AppID {2}",
                            progressIndex,
                            scanProgress.Maximum,
                            appId);
                    });

                    if (!File.Exists(schema))
                        continue;

                    try
                    {
                        GameEntry current = HistoryLoader.Load(file, schema, accountId, appId);

                        // A cache entry alone is not useful to this program.
                        // We only keep histories with at least one actual achievement timestamp.
                        if (current == null || current.Achievements.Count == 0)
                            continue;

                        GameEntry ownedGame;
                        if (libraryByApp.TryGetValue(appId, out ownedGame))
                        {
                            current.IsOwned = true;
                            current.AppType = ownedGame.AppType;
                        }

                        rawLocalByApp[appId] = current;
                    }
                    catch (Exception ex)
                    {
                        AppendLog("Error leyendo AppID " + appId + ": " + ex.Message);
                    }
                }

                if (cancelScanRequested)
                    return;

                // Resolve display names BEFORE snapshots are created.
                // Priority:
                //   1) public name already returned by SAM,
                //   2) cached public Steam AppID->name map,
                //   3) public Steam app catalog,
                //   4) schema/internal name only as a last-resort fallback.
                HashSet<uint> localIds = new HashSet<uint>(rawLocalByApp.Keys);
                Dictionary<uint, string> publicNames = ResolvePublicSteamNames(localIds, samNames);

                Dictionary<uint, GameEntry> localByApp = new Dictionary<uint, GameEntry>();

                BeginInvoke((MethodInvoker)delegate
                {
                    scanProgress.Style = ProgressBarStyle.Continuous;
                    scanProgress.Minimum = 0;
                    scanProgress.Maximum = Math.Max(1, rawLocalByApp.Count);
                    scanProgress.Value = 0;
                    lblScan.Text = "Protegiendo historiales…";
                });

                int protectedIndex = 0;

                foreach (GameEntry current in rawLocalByApp.Values)
                {
                    if (cancelScanRequested)
                        return;

                    string resolved;
                    if (publicNames.TryGetValue(current.AppId, out resolved) &&
                        !String.IsNullOrWhiteSpace(resolved))
                    {
                        current.GameName = resolved;
                    }
                    else if (String.IsNullOrWhiteSpace(current.GameName))
                    {
                        current.GameName = "AppID " + current.AppId.ToString(CultureInfo.InvariantCulture);
                    }

                    GameEntry protectedHistory = LoadOrCreateSnapshot(current);
                    localByApp[current.AppId] = protectedHistory;

                    protectedIndex++;
                    int copy = protectedIndex;
                    uint appCopy = current.AppId;

                    BeginInvoke((MethodInvoker)delegate
                    {
                        scanProgress.Value = Math.Min(scanProgress.Maximum, copy);
                        lblScan.Text = String.Format(
                            "Snapshots: {0}/{1} · {2}",
                            copy,
                            scanProgress.Maximum,
                            appCopy);
                    });
                }

                // The selector is intentionally limited to games with useful local history.
                // SAM's library list is still used for public names and Steam operations.
                List<GameEntry> merged = new List<GameEntry>();

                foreach (GameEntry local in localByApp.Values)
                    merged.Add(local);

                allGames = merged
                    .OrderBy(delegate(GameEntry g) { return g.GameName; })
                    .ThenBy(delegate(GameEntry g) { return g.AppId; })
                    .ToList();

                int resolvedCount = rawLocalByApp.Values.Count(delegate(GameEntry g)
                {
                    string n;
                    return publicNames.TryGetValue(g.AppId, out n) && !String.IsNullOrWhiteSpace(n);
                });

                BeginInvoke((MethodInvoker)delegate
                {
                    scanning = false;
                    btnScan.Text = "Escanear biblioteca";
                    scanProgress.Style = ProgressBarStyle.Continuous;
                    scanProgress.Value = scanProgress.Maximum;

                    lblScan.Text = String.Format(
                        "{0} historiales · {1}/{0} nombres públicos resueltos",
                        rawLocalByApp.Count,
                        resolvedCount);

                    lblStatus.Text = "Escaneo completado.";
                    cmbGames.Enabled = true;
                    RebuildGameList();
                });
            }
            catch (Exception ex)
            {
                AppendLog("ERROR DE ESCANEO: " + ex.Message);

                BeginInvoke((MethodInvoker)delegate
                {
                    scanning = false;
                    btnScan.Text = "Escanear biblioteca";
                    scanProgress.Style = ProgressBarStyle.Continuous;
                    scanProgress.Value = 0;
                    lblScan.Text = "Error.";
                    lblStatus.Text = "Error de escaneo.";
                    cmbGames.Enabled = true;

                    MessageBox.Show(
                        this,
                        ex.Message,
                        "Escaneo",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                });
            }
            finally
            {
                if (cancelScanRequested)
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        scanning = false;
                        btnScan.Text = "Escanear biblioteca";
                        scanProgress.Style = ProgressBarStyle.Continuous;
                        scanProgress.Value = 0;
                        lblScan.Text = "Escaneo cancelado.";
                        lblStatus.Text = "Escaneo cancelado.";
                        cmbGames.Enabled = true;
                    });
                }
            }
        }

        private void UpdateScanMarquee(string text)
        {
            if (IsDisposed)
                return;

            BeginInvoke((MethodInvoker)delegate
            {
                scanProgress.Style = ProgressBarStyle.Marquee;
                scanProgress.MarqueeAnimationSpeed = 25;
                lblScan.Text = text;
            });
        }

        private Dictionary<uint, string> ResolvePublicSteamNames(
            HashSet<uint> appIds,
            Dictionary<uint, string> samNames)
        {
            Dictionary<uint, string> result = new Dictionary<uint, string>();
            Dictionary<uint, string> cache = LoadSteamNameCache();

            foreach (uint appId in appIds)
            {
                string name;

                if (samNames != null &&
                    samNames.TryGetValue(appId, out name) &&
                    !String.IsNullOrWhiteSpace(name))
                {
                    result[appId] = name;
                    cache[appId] = name;
                    continue;
                }

                if (cache.TryGetValue(appId, out name) &&
                    !String.IsNullOrWhiteSpace(name))
                {
                    result[appId] = name;
                }
            }

            HashSet<uint> missing = new HashSet<uint>(
                appIds.Where(delegate(uint id) { return !result.ContainsKey(id); }));

            if (missing.Count == 0)
            {
                SaveSteamNameCache(cache);
                return result;
            }

            UpdateScanMarquee(
                "Resolviendo " + missing.Count.ToString(CultureInfo.InvariantCulture) +
                " nombres públicos en Steam…");

            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Encoding = Encoding.UTF8;
                    wc.Headers.Add(
                        HttpRequestHeader.UserAgent,
                        "AchievementTimelineReplay/0.3");

                    string body = wc.DownloadString(
                        "https://api.steampowered.com/ISteamApps/GetAppList/v2/");

                    JavaScriptSerializer parser = new JavaScriptSerializer();
                    parser.MaxJsonLength = Int32.MaxValue;

                    Dictionary<string, object> root =
                        parser.DeserializeObject(body) as Dictionary<string, object>;

                    if (root != null && root.ContainsKey("applist"))
                    {
                        Dictionary<string, object> applist =
                            root["applist"] as Dictionary<string, object>;

                        if (applist != null && applist.ContainsKey("apps"))
                        {
                            IEnumerable apps = applist["apps"] as IEnumerable;

                            if (apps != null)
                            {
                                foreach (object item in apps)
                                {
                                    if (missing.Count == 0)
                                        break;

                                    Dictionary<string, object> d =
                                        item as Dictionary<string, object>;

                                    if (d == null)
                                        continue;

                                    uint appId = GetJsonUInt(d, "appid", 0);

                                    if (appId == 0 || !missing.Contains(appId))
                                        continue;

                                    string name = RepairMojibake(GetJsonString(d, "name", ""));

                                    if (String.IsNullOrWhiteSpace(name))
                                        continue;

                                    result[appId] = name;
                                    cache[appId] = name;
                                    missing.Remove(appId);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog(
                    "No pude actualizar los nombres públicos desde Steam: " +
                    ex.Message +
                    ". Uso el caché disponible y, si falta, el nombre interno.");
            }

            SaveSteamNameCache(cache);
            return result;
        }

        private string SteamNameCachePath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AchievementTimelineReplay");

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            return Path.Combine(dir, "steam_public_names.tsv");
        }

        private Dictionary<uint, string> LoadSteamNameCache()
        {
            Dictionary<uint, string> result = new Dictionary<uint, string>();

            try
            {
                string path = SteamNameCachePath();

                if (!File.Exists(path))
                    return result;

                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    if (String.IsNullOrWhiteSpace(line))
                        continue;

                    int tab = line.IndexOf('\t');

                    if (tab <= 0)
                        continue;

                    uint appId;

                    if (!UInt32.TryParse(
                        line.Substring(0, tab),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out appId))
                        continue;

                    string name = line.Substring(tab + 1)
                        .Replace("\\t", "\t")
                        .Replace("\\n", "\n")
                        .Replace("\\r", "\r")
                        .Replace("\\\\", "\\");

                    name = RepairMojibake(name);

                    if (!String.IsNullOrWhiteSpace(name))
                        result[appId] = name;
                }
            }
            catch { }

            return result;
        }

        private void SaveSteamNameCache(Dictionary<uint, string> names)
        {
            try
            {
                List<string> lines = new List<string>();

                foreach (KeyValuePair<uint, string> kv in names.OrderBy(delegate(KeyValuePair<uint, string> x)
                {
                    return x.Key;
                }))
                {
                    string name = (kv.Value ?? "")
                        .Replace("\\", "\\\\")
                        .Replace("\t", "\\t")
                        .Replace("\r", "\\r")
                        .Replace("\n", "\\n");

                    lines.Add(
                        kv.Key.ToString(CultureInfo.InvariantCulture) +
                        "\t" +
                        name);
                }

                File.WriteAllLines(
                    SteamNameCachePath(),
                    lines.ToArray(),
                    Encoding.UTF8);
            }
            catch { }
        }

        private void RebuildGameList()
        {
            if (InvokeRequired)
            {
                BeginInvoke((MethodInvoker)delegate { RebuildGameList(); });
                return;
            }

            uint keep = currentGame == null ? 0 : currentGame.AppId;

            cmbGames.Items.Clear();

            IEnumerable<GameEntry> q = allGames
                .Where(delegate(GameEntry g) { return g.HasLocalHistory; });

            foreach (GameEntry g in q.OrderBy(delegate(GameEntry x) { return x.GameName; }))
                cmbGames.Items.Add(g);

            if (cmbGames.Items.Count == 0)
            {
                currentGame = null;
                currentOrder.Clear();
                grid.Rows.Clear();
                lblSnapshot.Text = "Snapshot: —";
                btnStart.Enabled = false;
                btnOpenSam.Enabled = false;
                btnQuerySteam.Enabled = false;
                return;
            }

            int index = 0;
            if (keep != 0)
            {
                for (int i = 0; i < cmbGames.Items.Count; i++)
                {
                    GameEntry g = cmbGames.Items[i] as GameEntry;
                    if (g != null && g.AppId == keep)
                    {
                        index = i;
                        break;
                    }
                }
            }

            cmbGames.SelectedIndex = index;
        }

        private GameEntry LoadOrCreateSnapshot(GameEntry current)
        {
            string dir = SnapshotDir(current.AccountId, current.AppId);
            string jsonPath = Path.Combine(dir, "snapshot.json");

            if (File.Exists(jsonPath))
            {
                try
                {
                    SnapshotData data = json.Deserialize<SnapshotData>(File.ReadAllText(jsonPath));
                    if (data != null && data.Achievements != null && data.Achievements.Count > 0)
                    {
                        GameEntry g = new GameEntry();
                        g.AppId = data.AppId;
                        g.AccountId = data.AccountId;
                        g.GameName = !String.IsNullOrWhiteSpace(current.GameName) ? current.GameName : data.GameName;
                        g.IsOwned = current.IsOwned;
                        g.HasLocalHistory = true;
                        g.HasSnapshot = true;
                        g.StatsPath = current.StatsPath;
                        g.SchemaPath = current.SchemaPath;

                        foreach (SnapshotAchievement s in data.Achievements)
                        {
                            AchievementRecord a = new AchievementRecord();
                            a.ApiId = s.ApiId;
                            a.DisplayName = s.DisplayName;
                            a.Description = s.Description;
                            a.OriginalUnix = s.OriginalUnix;
                            g.Achievements.Add(a);
                        }

                        if (!String.Equals(data.GameName, g.GameName, StringComparison.Ordinal))
                        {
                            try
                            {
                                data.GameName = g.GameName;
                                File.WriteAllText(jsonPath, json.Serialize(data), Encoding.UTF8);
                            }
                            catch { }
                        }

                        return g;
                    }
                }
                catch (Exception ex)
                {
                    AppendLog("Snapshot inválido para " + current.AppId + ": " + ex.Message);
                }
            }

            Directory.CreateDirectory(dir);

            SnapshotData snap = new SnapshotData();
            snap.Version = 1;
            snap.AppId = current.AppId;
            snap.AccountId = current.AccountId;
            snap.GameName = current.GameName;
            snap.CreatedAtUtc = DateTime.UtcNow;
            snap.Achievements = new List<SnapshotAchievement>();

            foreach (AchievementRecord a in current.Achievements)
            {
                SnapshotAchievement s = new SnapshotAchievement();
                s.ApiId = a.ApiId;
                s.DisplayName = a.DisplayName;
                s.Description = a.Description;
                s.OriginalUnix = a.OriginalUnix;
                snap.Achievements.Add(s);
            }

            File.WriteAllText(jsonPath, json.Serialize(snap), Encoding.UTF8);

            try
            {
                File.Copy(current.StatsPath, Path.Combine(dir, "original_UserGameStats.bin"), false);
            }
            catch { }

            try
            {
                File.Copy(current.SchemaPath, Path.Combine(dir, "original_UserGameStatsSchema.bin"), false);
            }
            catch { }

            current.HasSnapshot = true;
            current.HasLocalHistory = true;
            AppendLog("Snapshot protegido creado para " + current.GameName + " [" + current.AppId + "].");
            return current;
        }

        private string SnapshotDir(uint accountId, uint appId)
        {
            string baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AchievementTimelineReplay",
                "Snapshots");

            return Path.Combine(baseDir,
                accountId.ToString(CultureInfo.InvariantCulture) + "_" +
                appId.ToString(CultureInfo.InvariantCulture));
        }

        private void OnGameChanged()
        {
            currentGame = cmbGames.SelectedItem as GameEntry;
            currentOrder.Clear();
            grid.Rows.Clear();

            if (currentGame == null)
                return;

            btnOpenSam.Enabled = samMode == SamCliMode.Modern;
            btnQuerySteam.Enabled = currentGame.HasLocalHistory && samMode == SamCliMode.Modern;

            if (!currentGame.HasLocalHistory)
            {
                lblSnapshot.Text = "Snapshot: sin historial";
                lblOriginalDuration.Text = "Historial: no disponible";
                lblActualDuration.Text = "Reproducción: —";
                lblSelected.Text = "Seleccionados: —";

                btnStart.Enabled = false;
                btnRestore.Enabled = false;
                btnClearManual.Enabled = false;
                btnUp.Enabled = false;
                btnDown.Enabled = false;

                lblStatus.Text = "Este juego no tiene historial local. Usá “Abrir en SAM” para gestionarlo directamente.";
                return;
            }

            foreach (AchievementRecord a in currentGame.Achievements.OrderBy(delegate(AchievementRecord x) { return x.OriginalUnix; }))
            {
                AchievementRecord copy = a.Clone();
                copy.Included = true;
                copy.ManualGapSeconds = null;
                currentOrder.Add(copy);
            }

            long span = OriginalSpanSeconds();
            SetDurationSeconds(span);

            chkCustomOrder.Checked = false;
            btnRestore.Enabled = true;
            btnClearManual.Enabled = true;
            btnStart.Enabled = true;

            lblSnapshot.Text = currentGame.HasSnapshot ? "Snapshot: protegido ✓" : "Snapshot: no";
            lblOriginalDuration.Text = "Historial calendario: " + FormatSpan(span);

            PopulateGrid();
            AppendLog("Cargado " + currentGame.GameName + " [" + currentGame.AppId + "]: " +
                currentOrder.Count + " logros, span " + FormatSpan(span) + ".");
        }

        private long OriginalSpanSeconds()
        {
            List<AchievementRecord> list = currentOrder
                .Where(delegate(AchievementRecord a) { return a.Included; })
                .OrderBy(delegate(AchievementRecord a) { return a.OriginalUnix; })
                .ToList();

            if (list.Count < 2)
                return 0;

            return Math.Max(0, list[list.Count - 1].OriginalUnix - list[0].OriginalUnix);
        }

        private void SetDurationSeconds(long seconds)
        {
            if (seconds < 0)
                seconds = 0;

            long h = seconds / 3600;
            long m = (seconds % 3600) / 60;
            long s = seconds % 60;

            nudHours.Value = Math.Min(nudHours.Maximum, h);
            nudMinutes.Value = m;
            nudSeconds.Value = s;
        }

        private double TargetDurationSeconds()
        {
            return (double)nudHours.Value * 3600.0 +
                   (double)nudMinutes.Value * 60.0 +
                   (double)nudSeconds.Value;
        }

        private void UpdateAutomaticDurationFromSelection()
        {
            if (currentGame == null || !currentGame.HasLocalHistory)
                return;

            long selectedSpan = OriginalSpanSeconds();
            bool previousUpdating = updatingGrid;

            try
            {
                updatingGrid = true;
                SetDurationSeconds(selectedSpan);
            }
            finally
            {
                updatingGrid = previousUpdating;
            }
        }

        private Dictionary<AchievementRecord, ScheduleItem> CalculateSchedule()
        {
            Dictionary<AchievementRecord, ScheduleItem> result = new Dictionary<AchievementRecord, ScheduleItem>();

            List<AchievementRecord> replayOrder = currentOrder
                .Where(delegate(AchievementRecord a) { return a.Included; })
                .ToList();

            if (replayOrder.Count == 0)
                return result;

            List<AchievementRecord> chronological = replayOrder
                .OrderBy(delegate(AchievementRecord a) { return a.OriginalUnix; })
                .ToList();

            List<double> originalGaps = new List<double>();
            originalGaps.Add(0);

            for (int i = 1; i < chronological.Count; i++)
            {
                long gap = chronological[i].OriginalUnix - chronological[i - 1].OriginalUnix;
                originalGaps.Add(Math.Max(0.0, (double)gap));
            }

            double originalSpan = originalGaps.Sum();
            double targetSpan = TargetDurationSeconds();
            double factor = originalSpan > 0.0 ? targetSpan / originalSpan : 0.0;

            double cumulative = 0.0;

            for (int i = 0; i < replayOrder.Count; i++)
            {
                AchievementRecord a = replayOrder[i];
                double autoGap = i == 0 ? 0.0 : originalGaps[i] * factor;
                double gap = a.ManualGapSeconds.HasValue ? a.ManualGapSeconds.Value : autoGap;

                if (i == 0)
                    gap = 0.0;

                if (gap < 0.0)
                    gap = 0.0;

                cumulative += gap;

                ScheduleItem item = new ScheduleItem();
                item.Achievement = a;
                item.GapSeconds = gap;
                item.OffsetSeconds = cumulative;
                result[a] = item;
            }

            return result;
        }

        private Dictionary<AchievementRecord, double> OriginalGapsForSelection()
        {
            Dictionary<AchievementRecord, double> result = new Dictionary<AchievementRecord, double>();

            List<AchievementRecord> chronological = currentOrder
                .Where(delegate(AchievementRecord a) { return a.Included; })
                .OrderBy(delegate(AchievementRecord a) { return a.OriginalUnix; })
                .ToList();

            for (int i = 0; i < chronological.Count; i++)
            {
                double gap = i == 0 ? 0.0 : Math.Max(0.0, (double)(chronological[i].OriginalUnix - chronological[i - 1].OriginalUnix));
                result[chronological[i]] = gap;
            }

            return result;
        }

        private void PopulateGrid()
        {
            if (currentGame == null || !currentGame.HasLocalHistory)
                return;

            updatingGrid = true;

            int previousSelection = grid.SelectedRows.Count > 0 ? grid.SelectedRows[0].Index : -1;
            grid.Rows.Clear();

            Dictionary<AchievementRecord, ScheduleItem> schedule = CalculateSchedule();
            Dictionary<AchievementRecord, double> originalGaps = OriginalGapsForSelection();

            int displayOrder = 1;

            for (int i = 0; i < currentOrder.Count; i++)
            {
                AchievementRecord a = currentOrder[i];

                string originalGap = "—";
                string newGap = "—";
                string targetOffset = "—";
                string targetClock = "—";

                if (a.Included)
                {
                    double og;
                    if (originalGaps.TryGetValue(a, out og))
                        originalGap = FormatSpan((long)Math.Round(og));

                    ScheduleItem item;
                    if (schedule.TryGetValue(a, out item))
                    {
                        newGap = FormatSpan((long)Math.Round(item.GapSeconds));
                        targetOffset = FormatSpan((long)Math.Round(item.OffsetSeconds));
                        targetClock = DateTime.Now
                            .AddSeconds((double)nudInitialDelay.Value + item.OffsetSeconds)
                            .ToString("dd/MM HH:mm:ss");
                    }
                }

                int row = grid.Rows.Add(
                    a.Included,
                    a.Included ? displayOrder.ToString(CultureInfo.InvariantCulture) : "—",
                    a.DisplayName,
                    a.Description,
                    UnixToLocal(a.OriginalUnix).ToString("dd/MM/yyyy HH:mm:ss"),
                    originalGap,
                    newGap,
                    targetOffset,
                    targetClock,
                    a.SteamState
                );

                grid.Rows[row].Tag = a;

                if (a.ManualGapSeconds.HasValue)
                {
                    grid.Rows[row].Cells["newGap"].Style.BackColor = Color.LightGoldenrodYellow;
                    grid.Rows[row].Cells["targetOffset"].Style.BackColor = Color.LightGoldenrodYellow;
                }

                if (a.Included)
                    displayOrder++;
            }

            if (previousSelection >= 0 && previousSelection < grid.Rows.Count)
            {
                grid.ClearSelection();
                grid.Rows[previousSelection].Selected = true;
                grid.CurrentCell = grid.Rows[previousSelection].Cells["name"];
            }

            lblSelected.Text = "Seleccionados: " +
                currentOrder.Count(delegate(AchievementRecord a) { return a.Included; }).ToString(CultureInfo.InvariantCulture) +
                " / " + currentOrder.Count.ToString(CultureInfo.InvariantCulture);

            lblOriginalDuration.Text = "Historial calendario: " + FormatSpan(OriginalSpanSeconds());

            double actual = 0.0;
            if (schedule.Count > 0)
                actual = schedule.Values.Max(delegate(ScheduleItem x) { return x.OffsetSeconds; });

            lblActualDuration.Text = "Reproducción efectiva: " + FormatSpan((long)Math.Round(actual));

            updatingGrid = false;
        }

        private void GridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (updatingGrid || e.RowIndex < 0)
                return;

            if (e.ColumnIndex == grid.Columns["include"].Index)
            {
                AchievementRecord a = grid.Rows[e.RowIndex].Tag as AchievementRecord;
                if (a == null)
                    return;

                object value = grid.Rows[e.RowIndex].Cells["include"].Value;
                a.Included = value != null && Convert.ToBoolean(value, CultureInfo.InvariantCulture);

                UpdateAutomaticDurationFromSelection();
                RecalculatePlan();
            }
        }

        private void GridCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (updatingGrid || e.RowIndex < 0)
                return;

            string columnName = grid.Columns[e.ColumnIndex].Name;

            if (columnName != "newGap" && columnName != "targetOffset")
                return;

            AchievementRecord a = grid.Rows[e.RowIndex].Tag as AchievementRecord;
            if (a == null || !a.Included)
            {
                PopulateGrid();
                return;
            }

            string text = Convert.ToString(grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value, CultureInfo.InvariantCulture);

            if (String.IsNullOrWhiteSpace(text) || text.Trim() == "—")
            {
                a.ManualGapSeconds = null;
                PopulateGrid();
                return;
            }

            double seconds;
            if (!TryParseDuration(text, out seconds))
            {
                MessageBox.Show(this,
                    "Formato de tiempo no reconocido.\r\n\r\nEjemplos:\r\n" +
                    "00:30:00\r\n3d 22:24:20\r\n3.22:24:20\r\n90m\r\n1h 15m",
                    "Tiempo manual",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                PopulateGrid();
                return;
            }

            if (columnName == "newGap")
            {
                if (IsFirstIncluded(a))
                    a.ManualGapSeconds = null;
                else
                    a.ManualGapSeconds = Math.Max(0.0, seconds);
            }
            else
            {
                Dictionary<AchievementRecord, ScheduleItem> schedule = CalculateSchedule();
                AchievementRecord previous = PreviousIncluded(a);
                double previousOffset = 0.0;

                if (previous != null && schedule.ContainsKey(previous))
                    previousOffset = schedule[previous].OffsetSeconds;

                double gap = seconds - previousOffset;

                if (gap < 0.0)
                {
                    MessageBox.Show(this,
                        "Ese momento queda antes que el logro anterior.\r\nElegí un momento igual o posterior.",
                        "Tiempo manual",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    PopulateGrid();
                    return;
                }

                if (previous == null)
                    a.ManualGapSeconds = null;
                else
                    a.ManualGapSeconds = gap;
            }

            PopulateGrid();
        }

        private bool IsFirstIncluded(AchievementRecord a)
        {
            AchievementRecord first = currentOrder.FirstOrDefault(delegate(AchievementRecord x) { return x.Included; });
            return Object.ReferenceEquals(first, a);
        }

        private AchievementRecord PreviousIncluded(AchievementRecord a)
        {
            AchievementRecord previous = null;

            foreach (AchievementRecord item in currentOrder)
            {
                if (!item.Included)
                    continue;

                if (Object.ReferenceEquals(item, a))
                    return previous;

                previous = item;
            }

            return null;
        }

        private bool TryParseDuration(string raw, out double seconds)
        {
            seconds = 0.0;

            if (String.IsNullOrWhiteSpace(raw))
                return false;

            string s = raw.Trim().ToLowerInvariant().Replace(",", ".");

            Match dcolon = Regex.Match(s, @"^(\d+)\s*d\s+(\d{1,2}):(\d{1,2}):(\d{1,2})$");
            if (dcolon.Success)
            {
                seconds =
                    Double.Parse(dcolon.Groups[1].Value, CultureInfo.InvariantCulture) * 86400.0 +
                    Double.Parse(dcolon.Groups[2].Value, CultureInfo.InvariantCulture) * 3600.0 +
                    Double.Parse(dcolon.Groups[3].Value, CultureInfo.InvariantCulture) * 60.0 +
                    Double.Parse(dcolon.Groups[4].Value, CultureInfo.InvariantCulture);
                return true;
            }

            Match dotday = Regex.Match(s, @"^(\d+)\.(\d{1,2}):(\d{1,2}):(\d{1,2})$");
            if (dotday.Success)
            {
                seconds =
                    Double.Parse(dotday.Groups[1].Value, CultureInfo.InvariantCulture) * 86400.0 +
                    Double.Parse(dotday.Groups[2].Value, CultureInfo.InvariantCulture) * 3600.0 +
                    Double.Parse(dotday.Groups[3].Value, CultureInfo.InvariantCulture) * 60.0 +
                    Double.Parse(dotday.Groups[4].Value, CultureInfo.InvariantCulture);
                return true;
            }

            Match colon = Regex.Match(s, @"^(\d+):(\d{1,2}):(\d{1,2})$");
            if (colon.Success)
            {
                seconds =
                    Double.Parse(colon.Groups[1].Value, CultureInfo.InvariantCulture) * 3600.0 +
                    Double.Parse(colon.Groups[2].Value, CultureInfo.InvariantCulture) * 60.0 +
                    Double.Parse(colon.Groups[3].Value, CultureInfo.InvariantCulture);
                return true;
            }

            Match units = Regex.Match(s, @"^\s*(?:(\d+(?:\.\d+)?)\s*d)?\s*(?:(\d+(?:\.\d+)?)\s*h)?\s*(?:(\d+(?:\.\d+)?)\s*m)?\s*(?:(\d+(?:\.\d+)?)\s*s)?\s*$");
            if (units.Success &&
                (!String.IsNullOrEmpty(units.Groups[1].Value) ||
                 !String.IsNullOrEmpty(units.Groups[2].Value) ||
                 !String.IsNullOrEmpty(units.Groups[3].Value) ||
                 !String.IsNullOrEmpty(units.Groups[4].Value)))
            {
                double d = String.IsNullOrEmpty(units.Groups[1].Value) ? 0.0 : Double.Parse(units.Groups[1].Value, CultureInfo.InvariantCulture);
                double h = String.IsNullOrEmpty(units.Groups[2].Value) ? 0.0 : Double.Parse(units.Groups[2].Value, CultureInfo.InvariantCulture);
                double m = String.IsNullOrEmpty(units.Groups[3].Value) ? 0.0 : Double.Parse(units.Groups[3].Value, CultureInfo.InvariantCulture);
                double sec = String.IsNullOrEmpty(units.Groups[4].Value) ? 0.0 : Double.Parse(units.Groups[4].Value, CultureInfo.InvariantCulture);

                seconds = d * 86400.0 + h * 3600.0 + m * 60.0 + sec;
                return true;
            }

            return false;
        }

        private void RecalculatePlan()
        {
            if (currentGame == null || !currentGame.HasLocalHistory || updatingGrid)
                return;

            PopulateGrid();
        }

        private void GridMouseDown(object sender, MouseEventArgs e)
        {
            dragRowIndex = -1;
            liveDragging = false;
            liveDragRecord = null;
            dragStartPoint = e.Location;

            DataGridView.HitTestInfo hit = grid.HitTest(e.X, e.Y);

            if (e.Button == MouseButtons.Left && hit.RowIndex >= 0)
            {
                dragRowIndex = hit.RowIndex;
                liveDragRecord = grid.Rows[hit.RowIndex].Tag as AchievementRecord;
                grid.Cursor = Cursors.SizeAll;
            }
        }

        private void GridMouseMove(object sender, MouseEventArgs e)
        {
            DataGridView.HitTestInfo hit = grid.HitTest(e.X, e.Y);

            if (e.Button == MouseButtons.None)
            {
                liveDragging = false;
                liveDragRecord = null;
                dragRowIndex = -1;
                grid.Cursor = hit.RowIndex >= 0 ? Cursors.Hand : Cursors.Default;
                return;
            }

            if (e.Button != MouseButtons.Left || liveDragRecord == null)
                return;

            Rectangle dragRect = new Rectangle(
                dragStartPoint.X - SystemInformation.DragSize.Width / 2,
                dragStartPoint.Y - SystemInformation.DragSize.Height / 2,
                SystemInformation.DragSize.Width,
                SystemInformation.DragSize.Height);

            if (!liveDragging && dragRect.Contains(e.Location))
                return;

            liveDragging = true;
            grid.Cursor = Cursors.SizeAll;

            if (!chkCustomOrder.Checked)
                chkCustomOrder.Checked = true;

            if (hit.RowIndex < 0)
                return;

            int oldIndex = currentOrder.IndexOf(liveDragRecord);

            if (oldIndex < 0)
                return;

            Rectangle targetRect = grid.GetRowDisplayRectangle(hit.RowIndex, false);
            bool insertAfter = e.Y > targetRect.Top + (targetRect.Height / 2);
            int insertionIndex = hit.RowIndex + (insertAfter ? 1 : 0);

            // Account for removing the dragged row before inserting it again.
            if (oldIndex < insertionIndex)
                insertionIndex--;

            insertionIndex = Math.Max(0, Math.Min(currentOrder.Count - 1, insertionIndex));

            if (insertionIndex == oldIndex)
                return;

            currentOrder.RemoveAt(oldIndex);
            insertionIndex = Math.Max(0, Math.Min(currentOrder.Count, insertionIndex));
            currentOrder.Insert(insertionIndex, liveDragRecord);

            DataGridViewRow movingRow = grid.Rows[oldIndex];
            grid.Rows.RemoveAt(oldIndex);
            grid.Rows.Insert(insertionIndex, movingRow);

            dragRowIndex = insertionIndex;

            RefreshGridComputedValues();

            grid.ClearSelection();
            grid.Rows[insertionIndex].Selected = true;
            grid.CurrentCell = grid.Rows[insertionIndex].Cells["name"];
        }

        private void GridMouseUp(object sender, MouseEventArgs e)
        {
            DataGridView.HitTestInfo hit = grid.HitTest(e.X, e.Y);

            liveDragging = false;
            liveDragRecord = null;
            dragRowIndex = -1;
            grid.Cursor = hit.RowIndex >= 0 ? Cursors.Hand : Cursors.Default;
        }

        private void GridDragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
        }

        private void GridDragDrop(object sender, DragEventArgs e)
        {
            // Kept intentionally empty. v0.5 performs live row movement in MouseMove.
        }

        private void RefreshGridComputedValues()
        {
            if (currentGame == null || !currentGame.HasLocalHistory)
                return;

            bool previousUpdating = updatingGrid;
            updatingGrid = true;

            Dictionary<AchievementRecord, ScheduleItem> schedule = CalculateSchedule();
            Dictionary<AchievementRecord, double> originalGaps = OriginalGapsForSelection();

            int displayOrder = 1;

            for (int i = 0; i < grid.Rows.Count && i < currentOrder.Count; i++)
            {
                AchievementRecord a = currentOrder[i];
                DataGridViewRow row = grid.Rows[i];
                row.Tag = a;

                row.Cells["include"].Value = a.Included;
                row.Cells["order"].Value = a.Included
                    ? displayOrder.ToString(CultureInfo.InvariantCulture)
                    : "—";
                row.Cells["name"].Value = a.DisplayName;
                row.Cells["description"].Value = a.Description;
                row.Cells["original"].Value = UnixToLocal(a.OriginalUnix).ToString("dd/MM/yyyy HH:mm:ss");
                row.Cells["steam"].Value = a.SteamState;

                string originalGap = "—";
                string newGap = "—";
                string targetOffset = "—";
                string targetClock = "—";

                if (a.Included)
                {
                    double og;
                    if (originalGaps.TryGetValue(a, out og))
                        originalGap = FormatSpan((long)Math.Round(og));

                    ScheduleItem item;
                    if (schedule.TryGetValue(a, out item))
                    {
                        newGap = FormatSpan((long)Math.Round(item.GapSeconds));
                        targetOffset = FormatSpan((long)Math.Round(item.OffsetSeconds));
                        targetClock = DateTime.Now
                            .AddSeconds((double)nudInitialDelay.Value + item.OffsetSeconds)
                            .ToString("dd/MM HH:mm:ss");
                    }

                    displayOrder++;
                }

                row.Cells["originalGap"].Value = originalGap;
                row.Cells["newGap"].Value = newGap;
                row.Cells["targetOffset"].Value = targetOffset;
                row.Cells["targetTime"].Value = targetClock;

                row.Cells["newGap"].Style.BackColor = a.ManualGapSeconds.HasValue
                    ? Color.LightGoldenrodYellow
                    : Color.Empty;
                row.Cells["targetOffset"].Style.BackColor = a.ManualGapSeconds.HasValue
                    ? Color.LightGoldenrodYellow
                    : Color.Empty;
            }

            lblSelected.Text = "Seleccionados: " +
                currentOrder.Count(delegate(AchievementRecord a) { return a.Included; }).ToString(CultureInfo.InvariantCulture) +
                " / " + currentOrder.Count.ToString(CultureInfo.InvariantCulture);

            lblOriginalDuration.Text = "Historial calendario: " + FormatSpan(OriginalSpanSeconds());

            double actual = 0.0;
            if (schedule.Count > 0)
                actual = schedule.Values.Max(delegate(ScheduleItem x) { return x.OffsetSeconds; });

            lblActualDuration.Text = "Reproducción efectiva: " + FormatSpan((long)Math.Round(actual));

            updatingGrid = previousUpdating;
        }

        private void MoveSelected(int delta)
        {
            if (!chkCustomOrder.Checked || grid.SelectedRows.Count == 0)
                return;

            int index = grid.SelectedRows[0].Index;
            int target = index + delta;

            if (index < 0 || target < 0 || target >= currentOrder.Count)
                return;

            AchievementRecord item = currentOrder[index];
            currentOrder.RemoveAt(index);
            currentOrder.Insert(target, item);

            PopulateGrid();

            grid.ClearSelection();
            grid.Rows[target].Selected = true;
            grid.CurrentCell = grid.Rows[target].Cells["name"];
        }

        private void RestoreOriginalOrder()
        {
            currentOrder = currentOrder
                .OrderBy(delegate(AchievementRecord a) { return a.OriginalUnix; })
                .ToList();

            PopulateGrid();
        }

        private void ClearManualTimings()
        {
            foreach (AchievementRecord a in currentOrder)
                a.ManualGapSeconds = null;

            PopulateGrid();
        }

        private void QuerySteamState()
        {
            if (currentGame == null || !currentGame.HasLocalHistory)
                return;

            if (samMode != SamCliMode.Modern)
            {
                MessageBox.Show(this, "Esta función requiere el CLI moderno de SamRewritten.", "Steam", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnQuerySteam.Enabled = false;
            lblStatus.Text = "Consultando estado actual en Steam…";

            uint queryAppId = currentGame.AppId;
            string querySamPath = txtSam.Text.Trim();
            List<AchievementRecord> queryRecords = currentOrder.ToList();

            Thread t = new Thread(new ThreadStart(delegate
            {
                ProcessResult r = RunProcess(querySamPath,
                    "list-achievements " + queryAppId.ToString(CultureInfo.InvariantCulture),
                    90000);

                if (r.ExitCode == 0 && !r.TimedOut)
                {
                    Dictionary<string, string> states = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    foreach (Dictionary<string, object> d in ParseJsonObjectArray(r.StdOut))
                    {
                        string id = GetJsonString(d, "id", "");
                        bool achieved = GetJsonBool(d, "is_achieved", false);
                        object unlockObj = d.ContainsKey("unlock_time") ? d["unlock_time"] : null;

                        string state = achieved ? "Desbloqueado" : "Bloqueado";

                        if (achieved && unlockObj != null)
                        {
                            long ts = GetJsonLong(d, "unlock_time", 0);
                            if (ts > 0)
                                state = "✓ " + UnixToLocal(ts).ToString("dd/MM HH:mm");
                        }

                        if (!String.IsNullOrEmpty(id))
                            states[id] = state;
                    }

                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (currentGame != null && currentGame.AppId == queryAppId)
                        {
                            foreach (AchievementRecord a in queryRecords)
                            {
                                string state;
                                a.SteamState = states.TryGetValue(a.ApiId, out state) ? state : "No encontrado";
                            }

                            PopulateGrid();
                        }
                        btnQuerySteam.Enabled = true;
                        lblStatus.Text = "Estado de Steam actualizado.";
                    });
                }
                else
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        btnQuerySteam.Enabled = true;
                        lblStatus.Text = "No pude consultar Steam.";
                        MessageBox.Show(this, r.StdErr + "\r\n" + r.StdOut, "SamRewritten", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    });
                }
            }));

            t.IsBackground = true;
            t.Start();
        }

        private void OpenSelectedInSam()
        {
            if (currentGame == null)
                return;

            string cli = txtSam.Text.Trim();
            string dir = Path.GetDirectoryName(cli);
            string gui = "";

            try
            {
                if (!String.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    string[] candidates = Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly);

                    foreach (string f in candidates)
                    {
                        string name = Path.GetFileName(f).ToLowerInvariant();

                        if (name.Contains("samrewritten") &&
                            !name.Contains("cli"))
                        {
                            gui = f;
                            break;
                        }
                    }
                }
            }
            catch { }

            if (!String.IsNullOrEmpty(gui))
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = gui;
                    psi.Arguments = "--auto-open=" + currentGame.AppId.ToString(CultureInfo.InvariantCulture);
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Abrir SamRewritten", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            MessageBox.Show(this,
                "No encontré la interfaz gráfica de SamRewritten junto al CLI.\r\n\r\n" +
                "Abrí SamRewritten manualmente y buscá el AppID " +
                currentGame.AppId.ToString(CultureInfo.InvariantCulture) + ".",
                "Abrir en SAM",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void StartReplay()
        {
            if (running)
                return;

            if (currentGame == null || !currentGame.HasLocalHistory)
            {
                MessageBox.Show(this, "Este juego no tiene un historial local para migrar. Gestioná sus logros directamente desde SAM.", "Migración", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<AchievementRecord> selected = currentOrder
                .Where(delegate(AchievementRecord a) { return a.Included; })
                .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(this, "No hay logros seleccionados.", "Migración", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!ValidateSam(false))
            {
                MessageBox.Show(this, "Seleccioná y validá el ejecutable CLI de SamRewritten.", "SamRewritten", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (Process.GetProcessesByName("steam").Length == 0)
            {
                MessageBox.Show(this, "Steam no parece estar abierto. Abrilo e iniciá sesión antes de continuar.", "Steam", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Dictionary<AchievementRecord, ScheduleItem> schedule = CalculateSchedule();

            if (schedule.Count == 0)
                return;

            double finalOffset = schedule.Values.Max(delegate(ScheduleItem x) { return x.OffsetSeconds; });

            string summary = String.Format(
                "Juego: {0} [{1}]\r\n" +
                "Logros: {2}\r\n" +
                "Historial calendario original: {3}\r\n" +
                "Duración automática elegida: {4}\r\n" +
                "Duración efectiva con ajustes manuales: {5}\r\n" +
                "Orden: {6}\r\n" +
                "Modo reparación (bloquear antes): {7}\r\n" +
                "Registrar tiempo de juego (SAM idle): {8}\r\n\r\n" +
                "Esto modificará los logros de la cuenta de Steam actualmente iniciada. ¿Continuar?",
                currentGame.GameName,
                currentGame.AppId,
                selected.Count,
                FormatSpan(OriginalSpanSeconds()),
                FormatSpan((long)TargetDurationSeconds()),
                FormatSpan((long)Math.Round(finalOffset)),
                chkCustomOrder.Checked ? "personalizado" : "original",
                chkLockFirst.Checked ? "sí" : "no",
                chkIdleDuringMigration.Checked ? "sí" : "no");

            if (MessageBox.Show(this, summary, "Confirmar migración", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            ProcessResult verify = RunSamList(currentGame.AppId);

            if (verify.ExitCode != 0 || verify.TimedOut)
            {
                MessageBox.Show(this,
                    "SamRewritten no pudo leer los logros de este juego.\r\n\r\n" +
                    verify.StdErr + "\r\n" + verify.StdOut,
                    "SamRewritten",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            List<ScheduleItem> plan = schedule.Values
                .OrderBy(delegate(ScheduleItem x) { return x.OffsetSeconds; })
                .ToList();

            activeSamPath = txtSam.Text.Trim();
            activeSamMode = samMode;
            activeAppId = currentGame.AppId;
            activeGameName = currentGame.GameName;
            activeLockFirst = chkLockFirst.Checked;
            activeIdleDuringMigration = chkIdleDuringMigration.Checked;
            activeInitialDelaySeconds = (int)nudInitialDelay.Value;

            cancelRequested = false;
            running = true;

            SetEditingEnabled(false);
            btnStart.Enabled = false;
            btnCancel.Enabled = true;

            progress.Minimum = 0;
            progress.Maximum = plan.Count;
            progress.Value = 0;

            lblStatus.Text = "Preparando…";
            AppendLog("=== Inicio de migración: " + activeGameName + " ===");

            workerThread = new Thread(new ThreadStart(delegate { ReplayWorker(plan); }));
            workerThread.IsBackground = true;
            workerThread.Start();
        }

        private void ReplayWorker(List<ScheduleItem> plan)
        {
            Process idleProcess = null;

            try
            {
                // Repair mode modifies Steam state too. Do it BEFORE starting idle,
                // otherwise the one-shot lock command would immediately clear
                // Steam's "in-game" state.
                if (activeLockFirst)
                {
                    UpdateStatus("Bloqueando logros seleccionados…");

                    ProcessResult locked = RunSamSet(
                        activeAppId,
                        plan.Select(delegate(ScheduleItem x) { return x.Achievement.ApiId; }).ToList(),
                        false);

                    AppendProcess("LOCK", locked);

                    if (locked.TimedOut || locked.ExitCode != 0 || OutputContainsFailure(locked))
                        throw new Exception("SamRewritten informó un error al bloquear uno o más logros.");
                }

                if (cancelRequested)
                    return;

                idleProcess = StartSamIdle(activeAppId);

                if (activeIdleDuringMigration && activeSamMode == SamCliMode.Modern)
                {
                    Thread.Sleep(2000);

                    if (idleProcess == null || idleProcess.HasExited)
                        throw new Exception("No pude mantener activo SAM idle para registrar tiempo de juego.");
                }

                DateTime start = DateTime.Now.AddSeconds((double)activeInitialDelaySeconds);

                for (int i = 0; i < plan.Count; i++)
                    plan[i].Due = start.AddSeconds(plan[i].OffsetSeconds);

                for (int i = 0; i < plan.Count; i++)
                {
                    ScheduleItem item = plan[i];

                    while (!cancelRequested)
                    {
                        TimeSpan left = item.Due - DateTime.Now;

                        if (left.TotalMilliseconds <= 0)
                            break;

                        UpdateStatus(String.Format(
                            "Próximo: {0} en {1}",
                            item.Achievement.DisplayName,
                            FormatSpan((long)Math.Ceiling(left.TotalSeconds))));

                        Thread.Sleep((int)Math.Min(1000.0, Math.Max(100.0, left.TotalMilliseconds)));
                    }

                    if (cancelRequested)
                        return;

                    UpdateStatus("Desbloqueando: " + item.Achievement.DisplayName);

                    ProcessResult unlocked = RunSamSet(
                        activeAppId,
                        new List<string> { item.Achievement.ApiId },
                        true);

                    AppendProcess("UNLOCK " + item.Achievement.ApiId, unlocked);

                    if (unlocked.TimedOut || unlocked.ExitCode != 0 || OutputContainsFailure(unlocked))
                        throw new Exception("SamRewritten informó un error al desbloquear " + item.Achievement.ApiId + ".");

                    // Each one-shot unlock closes its Steam session and, in practice,
                    // clears the persistent idle presence too. Restore it immediately
                    // unless this was the final achievement.
                    if (i < plan.Count - 1)
                    {
                        idleProcess = RefreshSamIdleAfterCommand(idleProcess, activeAppId);
                        AppendLog("SAM idle restaurado después del logro.");
                    }

                    int done = i + 1;

                    BeginInvoke((MethodInvoker)delegate
                    {
                        progress.Value = Math.Min(progress.Maximum, done);
                    });
                }

                StopSamIdle(idleProcess);
                idleProcess = null;

                AppendLog("=== Migración completada: " + activeGameName + " ===");
                FinishReplay(false, null);
            }
            catch (Exception ex)
            {
                StopSamIdle(idleProcess);
                idleProcess = null;

                AppendLog("ERROR: " + ex.Message);
                FinishReplay(false, ex.Message);
            }
            finally
            {
                StopSamIdle(idleProcess);

                if (cancelRequested)
                {
                    AppendLog("=== Migración cancelada por el usuario ===");
                    FinishReplay(true, null);
                }
            }
        }

        private bool OutputContainsFailure(ProcessResult r)
        {
            string all = (r.StdOut + "\n" + r.StdErr).ToLowerInvariant();
            return all.Contains("\"success\": false") ||
                   all.Contains("\"success\":false") ||
                   all.Contains("failed to ");
        }

        private void FinishReplay(bool cancelled, string error)
        {
            BeginInvoke((MethodInvoker)delegate
            {
                running = false;

                SetEditingEnabled(true);

                btnStart.Enabled = currentGame != null && currentGame.HasLocalHistory;
                btnCancel.Enabled = false;

                if (cancelled)
                    lblStatus.Text = "Cancelado.";
                else if (!String.IsNullOrEmpty(error))
                    lblStatus.Text = "Error: " + error;
                else
                    lblStatus.Text = "Completado.";

                if (!Visible)
                    RestoreFromTray();

                trayIcon.Visible = true;
                trayIcon.BalloonTipTitle = "Achievement Timeline Replay";
                trayIcon.BalloonTipText = cancelled
                    ? "Migración cancelada."
                    : (!String.IsNullOrEmpty(error) ? "La migración terminó con error." : "Migración completada.");

                trayIcon.ShowBalloonTip(4000);
            });
        }

        private void CancelReplay()
        {
            if (!running)
                return;

            cancelRequested = true;
            lblStatus.Text = "Cancelando…";
        }

        private void UpdateStatus(string text)
        {
            if (IsDisposed)
                return;

            BeginInvoke((MethodInvoker)delegate { lblStatus.Text = text; });
        }

        private ProcessResult RunSamList(uint appId)
        {
            if (samMode == SamCliMode.Modern)
                return RunProcess(txtSam.Text.Trim(),
                    "list-achievements " + appId.ToString(CultureInfo.InvariantCulture),
                    60000);

            return RunProcess(txtSam.Text.Trim(),
                appId.ToString(CultureInfo.InvariantCulture) + " --ls --nostats",
                60000);
        }

        private Process StartSamIdle(uint appId)
        {
            return StartSamIdle(appId, true);
        }

        private Process StartSamIdle(uint appId, bool logMessage)
        {
            if (!activeIdleDuringMigration)
                return null;

            if (activeSamMode != SamCliMode.Modern)
            {
                AppendLog("SAM idle omitido: requiere el CLI moderno.");
                return null;
            }

            try
            {
                string comspec = Environment.GetEnvironmentVariable("COMSPEC");

                if (String.IsNullOrWhiteSpace(comspec))
                    comspec = "cmd.exe";

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = comspec;
                psi.Arguments =
                    "/D /S /C \"\"" +
                    activeSamPath +
                    "\" idle " +
                    appId.ToString(CultureInfo.InvariantCulture) +
                    "\"";
                psi.UseShellExecute = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.WorkingDirectory = Path.GetDirectoryName(activeSamPath);

                Process p = Process.Start(psi);

                if (p == null)
                    return null;

                if (logMessage)
                {
                    AppendLog(
                        "SAM idle iniciado vía cmd.exe para AppID " +
                        appId.ToString(CultureInfo.InvariantCulture) +
                        ".");
                }

                return p;
            }
            catch (Exception ex)
            {
                AppendLog("No pude iniciar SAM idle: " + ex.Message);
                return null;
            }
        }

        private void StopSamIdle(Process process)
        {
            StopSamIdle(process, true);
        }

        private void StopSamIdle(Process process, bool logMessage)
        {
            if (process == null)
                return;

            try
            {
                if (!process.HasExited)
                {
                    // Kill the whole process tree (cmd.exe + samrewritten-cli.exe).
                    ProcessStartInfo killPsi = new ProcessStartInfo();
                    killPsi.FileName = "taskkill.exe";
                    killPsi.Arguments =
                        "/PID " + process.Id.ToString(CultureInfo.InvariantCulture) +
                        " /T /F";
                    killPsi.UseShellExecute = false;
                    killPsi.CreateNoWindow = true;

                    using (Process killer = Process.Start(killPsi))
                    {
                        if (killer != null)
                            killer.WaitForExit(10000);
                    }

                    try
                    {
                        if (!process.HasExited)
                            process.Kill();
                    }
                    catch { }

                    try { process.WaitForExit(5000); } catch { }
                }

                if (logMessage)
                    AppendLog("SAM idle detenido.");
            }
            catch (Exception ex)
            {
                AppendLog("Aviso al detener SAM idle: " + ex.Message);
            }
            finally
            {
                try { process.Dispose(); } catch { }
            }
        }

        private Process RefreshSamIdleAfterCommand(Process idleProcess, uint appId)
        {
            if (!activeIdleDuringMigration || activeSamMode != SamCliMode.Modern)
                return idleProcess;

            // A one-shot SamRewritten command opens and closes its own Steam session.
            // On Windows this also clears the in-game state of the existing idle session.
            // Restarting idle immediately restores the persistent session.
            StopSamIdle(idleProcess, false);

            Process restarted = StartSamIdle(appId, false);

            if (restarted == null)
                throw new Exception("No pude reiniciar SAM idle después de modificar un logro.");

            Thread.Sleep(700);

            if (restarted.HasExited)
                throw new Exception("SAM idle se cerró al intentar restaurar el estado 'En ejecución'.");

            return restarted;
        }

        private ProcessResult RunSamSet(uint appId, List<string> ids, bool unlock)
        {
            string args;

            if (activeSamMode == SamCliMode.Modern)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(unlock ? "unlock " : "lock ");
                sb.Append(appId.ToString(CultureInfo.InvariantCulture));

                foreach (string id in ids)
                {
                    sb.Append(" ");
                    sb.Append(QuoteArg(id));
                }

                args = sb.ToString();
            }
            else
            {
                string joined = String.Join(",", ids.ToArray());
                args =
                    appId.ToString(CultureInfo.InvariantCulture) +
                    (unlock ? " --unlock " : " --lock ") +
                    QuoteArg(joined);
            }

            return RunProcess(activeSamPath, args, 90000);
        }

        private void SetEditingEnabled(bool enabled)
        {
            cmbGames.Enabled = enabled;
            txtSteam.Enabled = enabled;
            txtSam.Enabled = enabled;
            btnScan.Enabled = enabled;
            btnOpenSam.Enabled = enabled && currentGame != null && samMode == SamCliMode.Modern;
            btnQuerySteam.Enabled = enabled && currentGame != null && currentGame.HasLocalHistory && samMode == SamCliMode.Modern;
            chkCustomOrder.Enabled = enabled && currentGame != null && currentGame.HasLocalHistory;
            chkLockFirst.Enabled = enabled && currentGame != null && currentGame.HasLocalHistory;
            chkIdleDuringMigration.Enabled = enabled && currentGame != null && currentGame.HasLocalHistory && samMode == SamCliMode.Modern;
            nudHours.Enabled = enabled && currentGame != null && currentGame.HasLocalHistory;
            nudMinutes.Enabled = enabled && currentGame != null && currentGame.HasLocalHistory;
            nudSeconds.Enabled = enabled && currentGame != null && currentGame.HasLocalHistory;
            nudInitialDelay.Enabled = enabled && currentGame != null && currentGame.HasLocalHistory;
            grid.Enabled = enabled && currentGame != null && currentGame.HasLocalHistory;
            btnRestore.Enabled = enabled && currentGame != null && currentGame.HasLocalHistory;
            btnClearManual.Enabled = enabled && currentGame != null && currentGame.HasLocalHistory;
            btnUp.Enabled = enabled && chkCustomOrder.Checked && currentGame != null && currentGame.HasLocalHistory;
            btnDown.Enabled = btnUp.Enabled;
        }

        private string QuoteArg(string s)
        {
            if (s == null)
                return "\"\"";

            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private ProcessResult RunProcess(string file, string args, int timeoutMs)
        {
            ProcessResult r = new ProcessResult();
            r.ExitCode = -1;

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = file;
                psi.Arguments = args;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
                psi.WorkingDirectory = Path.GetDirectoryName(file);

                using (Process p = new Process())
                {
                    p.StartInfo = psi;
                    p.Start();

                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();

                    if (!p.WaitForExit(timeoutMs))
                    {
                        r.TimedOut = true;
                        try { p.Kill(); } catch { }
                    }
                    else
                    {
                        r.ExitCode = p.ExitCode;
                    }

                    r.StdOut = stdout;
                    r.StdErr = stderr;
                }
            }
            catch (Exception ex)
            {
                r.StdErr = ex.ToString();
            }

            return r;
        }

        private List<Dictionary<string, object>> ParseJsonObjectArray(string text)
        {
            List<Dictionary<string, object>> result = new List<Dictionary<string, object>>();

            object obj = json.DeserializeObject(text);
            object[] arr = obj as object[];

            if (arr == null)
                return result;

            foreach (object item in arr)
            {
                Dictionary<string, object> d = item as Dictionary<string, object>;
                if (d != null)
                    result.Add(d);
            }

            return result;
        }

        private static string GetJsonString(Dictionary<string, object> d, string key, string fallback)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null)
                return fallback;

            return Convert.ToString(d[key], CultureInfo.InvariantCulture);
        }

        private static uint GetJsonUInt(Dictionary<string, object> d, string key, uint fallback)
        {
            long v = GetJsonLong(d, key, fallback);
            if (v < 0 || v > UInt32.MaxValue)
                return fallback;
            return (uint)v;
        }

        private static long GetJsonLong(Dictionary<string, object> d, string key, long fallback)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null)
                return fallback;

            try
            {
                return Convert.ToInt64(d[key], CultureInfo.InvariantCulture);
            }
            catch
            {
                long parsed;
                if (Int64.TryParse(Convert.ToString(d[key], CultureInfo.InvariantCulture), out parsed))
                    return parsed;
            }

            return fallback;
        }

        private static bool GetJsonBool(Dictionary<string, object> d, string key, bool fallback)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null)
                return fallback;

            try
            {
                return Convert.ToBoolean(d[key], CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static string RepairMojibake(string value)
        {
            if (String.IsNullOrEmpty(value))
                return value;

            int before = MojibakeScore(value);

            if (before == 0)
                return value;

            try
            {
                Encoding windows1252 = Encoding.GetEncoding(1252);
                byte[] bytes = windows1252.GetBytes(value);
                string repaired = Encoding.UTF8.GetString(bytes);

                if (MojibakeScore(repaired) < before)
                    return repaired;
            }
            catch { }

            return value;
        }

        private static int MojibakeScore(string value)
        {
            if (String.IsNullOrEmpty(value))
                return 0;

            int score = 0;

            foreach (char c in value)
            {
                if (c == 'Ã' ||
                    c == 'Â' ||
                    c == 'â' ||
                    c == 'ð' ||
                    c == '�')
                    score++;
            }

            // Common Windows-1252 fragments produced by UTF-8 decoded as ANSI.
            string[] fragments = new string[]
            {
                "â„¢", "â€“", "â€”", "â€™", "â€œ", "â€", "Â®", "Â©"
            };

            foreach (string fragment in fragments)
            {
                int start = 0;
                while (true)
                {
                    int found = value.IndexOf(fragment, start, StringComparison.Ordinal);
                    if (found < 0)
                        break;

                    score += 3;
                    start = found + fragment.Length;
                }
            }

            return score;
        }

        private void AppendProcess(string label, ProcessResult r)
        {
            AppendLog("[" + label + "] exit=" + r.ExitCode + (r.TimedOut ? " TIMEOUT" : ""));

            if (!String.IsNullOrWhiteSpace(r.StdOut))
                AppendLog(r.StdOut.Trim());

            if (!String.IsNullOrWhiteSpace(r.StdErr))
                AppendLog(r.StdErr.Trim());
        }

        private void AppendLog(string text)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke((MethodInvoker)delegate { AppendLog(text); });
                return;
            }

            lock (logLock)
            {
                txtLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine);
            }
        }

        private static DateTime UnixToLocal(long unix)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            try
            {
                return epoch.AddSeconds(unix).ToLocalTime();
            }
            catch
            {
                return epoch.ToLocalTime();
            }
        }

        private static string FormatSpan(long totalSeconds)
        {
            if (totalSeconds < 0)
                totalSeconds = 0;

            long d = totalSeconds / 86400;
            long h = (totalSeconds % 86400) / 3600;
            long m = (totalSeconds % 3600) / 60;
            long s = totalSeconds % 60;

            if (d > 0)
                return String.Format("{0}d {1:00}:{2:00}:{3:00}", d, h, m, s);

            return String.Format("{0:00}:{1:00}:{2:00}", h, m, s);
        }

        private uint GetMostRecentAccountId(string steamRoot)
        {
            try
            {
                string file = Path.Combine(steamRoot, "config", "loginusers.vdf");

                if (!File.Exists(file))
                    return 0;

                string text = File.ReadAllText(file);

                MatchCollection matches = Regex.Matches(
                    text,
                    "\"(\\d{17})\"\\s*\\{(.*?)\\}",
                    RegexOptions.Singleline);

                foreach (Match m in matches)
                {
                    if (Regex.IsMatch(m.Groups[2].Value, "\"MostRecent\"\\s*\"1\"", RegexOptions.IgnoreCase))
                    {
                        ulong steamId = UInt64.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                        const ulong Base = 76561197960265728UL;

                        if (steamId >= Base)
                            return (uint)(steamId - Base);
                    }
                }
            }
            catch { }

            return 0;
        }

        private bool TryParseStatsFilename(string file, out uint accountId, out uint appId)
        {
            accountId = 0;
            appId = 0;

            Match m = Regex.Match(
                Path.GetFileName(file),
                "^UserGameStats_(\\d+)_(\\d+)\\.bin$",
                RegexOptions.IgnoreCase);

            if (!m.Success)
                return false;

            return UInt32.TryParse(m.Groups[1].Value, out accountId) &&
                   UInt32.TryParse(m.Groups[2].Value, out appId);
        }

        private void HideToTray()
        {
            trayIcon.Visible = true;
            Hide();

            trayIcon.BalloonTipTitle = "Achievement Timeline Replay";
            trayIcon.BalloonTipText = "La migración sigue ejecutándose en segundo plano.";
            trayIcon.ShowBalloonTip(2500);
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            trayIcon.Visible = running;
        }

        private void ExitFromTray()
        {
            if (running)
            {
                if (MessageBox.Show(this,
                    "Hay una migración activa. Salir la cancelará.\r\n\r\n¿Salir?",
                    "Confirmar",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                cancelRequested = true;
            }

            trayIcon.Visible = false;
            running = false;
            Application.Exit();
        }

        private void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSettings();

            if (running && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
            }
            else
            {
                trayIcon.Visible = false;
            }
        }

        private string SettingsPath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AchievementTimelineReplay");

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            return Path.Combine(dir, "settings.txt");
        }

        private void LoadSettings()
        {
            try
            {
                string file = SettingsPath();

                if (!File.Exists(file))
                    return;

                foreach (string line in File.ReadAllLines(file))
                {
                    int eq = line.IndexOf('=');

                    if (eq <= 0)
                        continue;

                    string key = line.Substring(0, eq);
                    string value = line.Substring(eq + 1);

                    if (key == "steam")
                        txtSteam.Text = value;

                    if (key == "sam")
                        txtSam.Text = value;
                }
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                File.WriteAllLines(
                    SettingsPath(),
                    new string[]
                    {
                        "steam=" + txtSteam.Text.Trim(),
                        "sam=" + txtSam.Text.Trim()
                    });
            }
            catch { }
        }
    }

    internal static class HistoryLoader
    {
        public static GameEntry Load(string statsPath, string schemaPath, uint accountId, uint appId)
        {
            Dictionary<string, object> schemaRoot = BinaryKvReader.ReadFile(schemaPath);
            Dictionary<string, object> statsRoot = BinaryKvReader.ReadFile(statsPath);

            Dictionary<string, object> app = GetDict(schemaRoot, appId.ToString(CultureInfo.InvariantCulture));
            Dictionary<string, object> schemaStats = GetDict(app, "stats");
            Dictionary<string, object> cache = GetDict(statsRoot, "cache");

            GameEntry game = new GameEntry();
            game.AppId = appId;
            game.AccountId = accountId;
            game.StatsPath = statsPath;
            game.SchemaPath = schemaPath;
            game.GameName = GetString(app, "gamename", appId.ToString(CultureInfo.InvariantCulture));
            game.HasLocalHistory = true;

            foreach (KeyValuePair<string, object> groupEntry in schemaStats)
            {
                Dictionary<string, object> group = groupEntry.Value as Dictionary<string, object>;

                if (group == null)
                    continue;

                string type = GetString(group, "type", "");

                if (!String.Equals(type, "ACHIEVEMENTS", StringComparison.OrdinalIgnoreCase))
                    continue;

                Dictionary<string, object> bits = GetDictOrNull(group, "bits");
                Dictionary<string, object> cacheGroup = GetDictOrNull(cache, groupEntry.Key);

                if (bits == null || cacheGroup == null)
                    continue;

                Dictionary<string, object> times = GetDictOrNull(cacheGroup, "AchievementTimes");

                if (times == null)
                    continue;

                foreach (KeyValuePair<string, object> bitEntry in bits)
                {
                    Dictionary<string, object> bit = bitEntry.Value as Dictionary<string, object>;

                    if (bit == null)
                        continue;

                    long ts = GetLong(times, bitEntry.Key, 0);

                    if (ts <= 0)
                        continue;

                    AchievementRecord a = new AchievementRecord();
                    a.ApiId = GetString(bit, "name", "bit_" + bitEntry.Key);
                    a.DisplayName = GetDisplayText(bit, "name", a.ApiId);
                    a.Description = GetDisplayText(bit, "desc", "");
                    a.OriginalUnix = ts;
                    a.Included = true;

                    game.Achievements.Add(a);
                }
            }

            game.Achievements = game.Achievements
                .OrderBy(delegate(AchievementRecord a) { return a.OriginalUnix; })
                .ToList();

            return game;
        }

        private static string GetDisplayText(Dictionary<string, object> bit, string field, string fallback)
        {
            Dictionary<string, object> display = GetDictOrNull(bit, "display");
            Dictionary<string, object> values = display == null ? null : GetDictOrNull(display, field);

            if (values != null)
            {
                string[] preferred = new string[] { "latam", "spanish", "english" };

                foreach (string lang in preferred)
                {
                    string value = GetString(values, lang, "");
                    if (!String.IsNullOrEmpty(value))
                        return value;
                }

                foreach (KeyValuePair<string, object> kv in values)
                {
                    string s = kv.Value as string;

                    if (!String.IsNullOrEmpty(s) &&
                        !String.Equals(kv.Key, "token", StringComparison.OrdinalIgnoreCase))
                        return s;
                }
            }

            return fallback;
        }

        private static Dictionary<string, object> GetDict(Dictionary<string, object> d, string key)
        {
            Dictionary<string, object> result = GetDictOrNull(d, key);

            if (result == null)
                throw new InvalidDataException("Falta la sección '" + key + "'.");

            return result;
        }

        private static Dictionary<string, object> GetDictOrNull(Dictionary<string, object> d, string key)
        {
            if (d == null || !d.ContainsKey(key))
                return null;

            return d[key] as Dictionary<string, object>;
        }

        private static string GetString(Dictionary<string, object> d, string key, string fallback)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null)
                return fallback;

            string s = d[key] as string;
            return s ?? Convert.ToString(d[key], CultureInfo.InvariantCulture);
        }

        private static long GetLong(Dictionary<string, object> d, string key, long fallback)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null)
                return fallback;

            object v = d[key];

            if (v is long)
                return (long)v;

            if (v is ulong)
                return unchecked((long)(ulong)v);

            if (v is int)
                return (int)v;

            if (v is uint)
                return (uint)v;

            long parsed;

            if (Int64.TryParse(
                Convert.ToString(v, CultureInfo.InvariantCulture),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed))
                return parsed;

            return fallback;
        }
    }

    internal sealed class BinaryKvReader
    {
        private BinaryReader reader;

        private BinaryKvReader(Stream stream)
        {
            reader = new BinaryReader(stream, Encoding.UTF8);
        }

        public static Dictionary<string, object> ReadFile(string path)
        {
            using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                BinaryKvReader r = new BinaryKvReader(fs);
                return r.ReadObject();
            }
        }

        private Dictionary<string, object> ReadObject()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                byte type = reader.ReadByte();

                if (type == 8)
                    break;

                string key = ReadCString();
                object value;

                switch (type)
                {
                    case 0:
                        value = ReadObject();
                        break;

                    case 1:
                        value = ReadCString();
                        break;

                    case 2:
                        value = (long)reader.ReadUInt32();
                        break;

                    case 3:
                        value = reader.ReadSingle();
                        break;

                    case 4:
                        value = (long)reader.ReadUInt32();
                        break;

                    case 5:
                        value = ReadWideString();
                        break;

                    case 6:
                        value = (long)reader.ReadUInt32();
                        break;

                    case 7:
                        value = reader.ReadUInt64();
                        break;

                    case 9:
                        value = (long)reader.ReadByte();
                        break;

                    case 10:
                        value = 0L;
                        break;

                    case 11:
                        value = 1L;
                        break;

                    default:
                        throw new InvalidDataException(
                            "Tipo Binary KeyValues no soportado: 0x" +
                            type.ToString("X2") +
                            " en '" + key + "'.");
                }

                dict[key] = value;
            }

            return dict;
        }

        private string ReadCString()
        {
            List<byte> bytes = new List<byte>();

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                byte b = reader.ReadByte();

                if (b == 0)
                    break;

                bytes.Add(b);
            }

            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        private string ReadWideString()
        {
            ushort chars = reader.ReadUInt16();

            if (chars == 0)
                return "";

            byte[] bytes = reader.ReadBytes(chars * 2);
            string s = Encoding.Unicode.GetString(bytes);
            return s.TrimEnd('\0');
        }
    }
}
