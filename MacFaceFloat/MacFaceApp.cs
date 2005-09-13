/*
 * MacFace アプリケーションクラス
 *
 * $Id$
 * 
 * project created on 2004/06/02 at 2:43
 * 
 */
using System;
using System.Windows.Forms;
using System.Drawing;
using System.Data;
using System.IO;
using System.Reflection;

namespace MacFace.FloatApp
{
	/// <summary>
	/// MacFaceApp の概要の説明です。
	/// </summary>
	public class MacFaceApp : ApplicationContext
	{
		private const string MES_OPEN_PATTERN_WINDOW = "パターンウインドウを開く(&P)";
		private const string MES_CLOSE_PATTERN_WINDOW = "パターンウインドウを閉じる(&P)";
		private const string MES_OPEN_STATUS_WINDOW = "ステータスウインドウを開く(&S)";
		private const string MES_CLOSE_STATUS_WINDOW = "ステータスウインドウを閉じる(&S)";

		private Configuration config;

		private CPUStatistics cpuStats;
		private MemoryStatistics memStats;

		private System.Windows.Forms.Timer updateTimer;

		private NotifyIcon notifyIcon;
		private PatternWindow patternWindow;
		private StatusWindow statusWindow;
		private MenuItem menuItemTogglePatternWindow;
		private MenuItem menuItemToggleStatusWindow;

		[STAThread]
		public static void Main(string[] args)
		{
			MacFaceApp app = new MacFaceApp();
			app.StartApplication();
		}

		public MacFaceApp()
		{
			config = Configuration.GetInstance();
			config.Load();

			cpuStats = new CPUStatistics(61);
			memStats = new MemoryStatistics(61);

			updateTimer = new System.Windows.Forms.Timer();
			updateTimer.Enabled = false;
			updateTimer.Interval = 1000;
			updateTimer.Tick += new EventHandler(this.CountProcessorUsage);

			patternWindow = null;
			statusWindow = null;

			InitializeComponent();
		}

		void InitializeComponent() 
		{
			// コンテキストメニュー
			ContextMenu contextMenu = new System.Windows.Forms.ContextMenu();
			menuItemTogglePatternWindow = new System.Windows.Forms.MenuItem();
			menuItemToggleStatusWindow = new System.Windows.Forms.MenuItem();
			MenuItem menuItemConfigure = new System.Windows.Forms.MenuItem();
			MenuItem menuItemExit = new System.Windows.Forms.MenuItem();
			MenuItem menuVersionInfo = new System.Windows.Forms.MenuItem();

			menuItemTogglePatternWindow.Text = MES_OPEN_PATTERN_WINDOW;
			menuItemTogglePatternWindow.Click +=new EventHandler(menuItemTogglePatternWindow_Click);

			menuItemToggleStatusWindow.Text = MES_OPEN_STATUS_WINDOW;
			menuItemToggleStatusWindow.Click +=new EventHandler(menuItemToggleStatusWindow_Click);

			menuItemConfigure.Text = "MacFace の設定(&O)...";
			menuItemConfigure.Click +=new EventHandler(menuItemConfigure_Click);

			menuVersionInfo.Index = 0;
			menuVersionInfo.Text = "バージョン情報(&A)";
			menuVersionInfo.Click +=new EventHandler(menuVersionInfo_Click);

			menuItemExit.Index = 0;
			menuItemExit.Text = "終了(&X)";
			menuItemExit.Click += new System.EventHandler(menuItemExit_Click);

			contextMenu.MenuItems.AddRange(new MenuItem[] {
					menuItemTogglePatternWindow,
					menuItemToggleStatusWindow,
					new MenuItem("-"),
					menuItemConfigure,
					menuVersionInfo,
					new MenuItem("-"),
					menuItemExit}
				);

			// 通知アイコン
			Assembly asm = Assembly.GetExecutingAssembly();
			this.notifyIcon = new System.Windows.Forms.NotifyIcon();
			this.notifyIcon.Text = "MacFace";
			this.notifyIcon.Icon = new Icon(asm.GetManifestResourceStream("MacFace.FloatApp.App.ico"));
			this.notifyIcon.Visible = true;
			this.notifyIcon.ContextMenu = contextMenu;
		}

		public void StartApplication()
		{
			// 顔パターン読み込み
			bool result = false;
			if (Directory.Exists(config.FaceDefPath))
			{
				result = LoadFaceDefine(config.FaceDefPath);
			}

			if (!result)
			{
				string path = Path.Combine(Application.StartupPath, "default.plist");

				if (!LoadFaceDefine(path))
				{
					Application.Exit();
					return;
				}
			}

			if (config.ShowPatternWindow) 
			{
				openPatternWindow();
			}

			if (config.ShowStatusWindow) 
			{
				openStatusWindow();
			}

			updateTimer.Start();

			Application.ApplicationExit += new EventHandler(Application_ApplicationExit);
			Application.Run(this);
		}

		void Application_ApplicationExit(object sender, EventArgs e)
		{
			notifyIcon.Visible = false;

			config.Save();
		}

		public bool LoadFaceDefine(string path)
		{
			FaceDef newFaceDef = null;
			string plistPath = Path.Combine(path, "faceDef.plist");

			if (!File.Exists(plistPath))
			{
				System.Windows.Forms.MessageBox.Show(
					String.Format("指定されたフォルダに顔パターン定義XMLファイル \"faceDef.plist\" が存在しません。\n\nフォルダ:\n{0}", path),
					"MacFace for Windows", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}
			
			try 
			{
				newFaceDef = new MacFace.FaceDef(path);
			} 
			catch (System.IO.IOException ie) 
			{
				System.Windows.Forms.MessageBox.Show(
					String.Format("顔パターン定義XMLファイルを読み込む際にエラーが発生しました。\n\n原因:\n{0}",
					ie.ToString()), "MacFace for Windows", MessageBoxButtons.OK, MessageBoxIcon.Error);
				
				return false;
			}
			catch (System.Xml.XmlException xe) 
			{
				System.Windows.Forms.MessageBox.Show(
					String.Format("顔パターン定義XMLファイルを読込み中にエラーが発生しました。\n\n原因:\n{0}",
					xe.ToString()), "MacFace for Windows", MessageBoxButtons.OK, MessageBoxIcon.Error);
				
				return false;
			}

			if (patternWindow != null) 
			{
				// 顔パターン差し替え中は更新を止めておく
				if (updateTimer != null) updateTimer.Stop();

				patternWindow.FaceDef = newFaceDef;
				patternWindow.Refresh();

				notifyIcon.Text = "MacFace - " + patternWindow.FaceDef.Title;

				// 更新再開
				if (updateTimer != null) updateTimer.Start();
			}

			return true;
		}

		public void CountProcessorUsage(object sender, EventArgs e)
		{
			cpuStats.Update();
			CPUUsage cpuUsage = cpuStats.Latest;

			memStats.Update();
			MemoryUsage memUsage = memStats.Latest;
			
			if (patternWindow != null) 
			{
				FaceDef.PatternSuite suite = FaceDef.PatternSuite.Normal;

				int pattern = cpuUsage.Active / 10;
				int avilable = (int)memStats.TotalVisibleMemorySize * 1024 - memUsage.Committed;
				if (avilable < (10 * 1024 *1024)) 
				{
					suite = FaceDef.PatternSuite.MemoryInsufficient;
				} 
				else if (memUsage.Available < (30 * 1024 *1024)) 
				{
					suite = FaceDef.PatternSuite.MemoryDecline;
				}

				int markers = FaceDef.MarkerNone;
				if (memUsage.Pagein > 0) markers += FaceDef.MarkerPageIn;
				if (memUsage.Pageout > 0) markers += FaceDef.MarkerPageOut;

				patternWindow.UpdatePattern(suite, pattern, markers);
			}

			if (statusWindow != null) 
			{
				statusWindow.UpdateGraph();
			}
		}

		public void openPatternWindow()
		{
			// パターンウインドウ
			patternWindow = new PatternWindow();
			patternWindow.Closed += new EventHandler(patternWindow_Closed);
			patternWindow.Move +=new EventHandler(patternWindow_Move);

			patternWindow.Location = config.Location;
			patternWindow.Opacity = (float)config.Opacity / 100;
			patternWindow.PatternSize = (float)config.PatternSize / 100;
			patternWindow.TransparentMouseMessage = config.TransparentMouseMessage;

			LoadFaceDefine(config.FaceDefPath);

			patternWindow.Show();

			menuItemTogglePatternWindow.Text = MES_CLOSE_PATTERN_WINDOW;
			config.ShowPatternWindow = true;
		}

		public void openStatusWindow()
		{
			statusWindow = new StatusWindow(cpuStats, memStats);
			statusWindow.Closed += new EventHandler(statusWindow_Closed);
			statusWindow.Move +=new EventHandler(statusWindow_Move);

			statusWindow.StartPosition = FormStartPosition.Manual;
			statusWindow.Location = config.StatusWindowLocation;

			statusWindow.UpdateGraph();
			statusWindow.Show();

			menuItemToggleStatusWindow.Text = MES_CLOSE_STATUS_WINDOW;
			config.ShowStatusWindow = true;
		}

		/*
		 * メニュークリックイベント
		 */

		public void menuItemExit_Click(object sender, System.EventArgs e)
		{
			updateTimer.Stop();
			ExitThread();
		}

		private void menuItemConfigure_Click(object sender, EventArgs e)
		{
			ConfigurationForm configForm = new ConfigurationForm();
			configForm.ConfigChanged += new ConfigChangedEvent(configForm_ConfigChanged);
			configForm.Show();
		}

		private void configForm_ConfigChanged()
		{
			if (patternWindow.FaceDef.Path != config.FaceDefPath) 
			{
				bool result = LoadFaceDefine(config.FaceDefPath);
				// パターン変更に失敗したら設定を元に戻す
				if (!result) 
				{
					config.FaceDefPath = patternWindow.FaceDef.Path;
				}
			}

			if (patternWindow != null) 
			{
				patternWindow.Opacity = (float)config.Opacity / 100;
				patternWindow.PatternSize = (float)config.PatternSize / 100;
				patternWindow.TransparentMouseMessage = config.TransparentMouseMessage;
				patternWindow.Refresh();
			}
		}

		private void patternWindow_Closed(object sender, EventArgs e)
		{
			patternWindow.Dispose();
			patternWindow = null;
			menuItemTogglePatternWindow.Text = MES_OPEN_PATTERN_WINDOW;
			config.ShowPatternWindow = false;
		}

		private void statusWindow_Closed(object sender, EventArgs e)
		{
			statusWindow.Dispose();
			statusWindow = null;
			menuItemToggleStatusWindow.Text = MES_OPEN_STATUS_WINDOW;
			config.ShowStatusWindow = false;
		}

		private void menuItemTogglePatternWindow_Click(object sender, EventArgs e)
		{
			if (patternWindow == null) 
			{
				openPatternWindow();
			} 
			else 
			{
				patternWindow.Close();
			}
		}

		private void menuItemToggleStatusWindow_Click(object sender, EventArgs e)
		{
			if (statusWindow == null) 
			{
				openStatusWindow();
			}
			else 
			{
				statusWindow.Close();
			}
		}

		private void patternWindow_Move(object sender, EventArgs e)
		{
			config.Location = patternWindow.Location;
		}

		private void statusWindow_Move(object sender, EventArgs e)
		{
			config.StatusWindowLocation = statusWindow.Location;
		}

		private void menuVersionInfo_Click(object sender, EventArgs e)
		{
			InfoWindow window = new InfoWindow();
			window.Show();
		}
	}
}
