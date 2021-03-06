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
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

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
		private int pageio_count;

		private System.Windows.Forms.Timer updateTimer;

		private NotifyIcon notifyIcon;
		private PatternWindow patternWindow;
		private StatusWindow statusWindow;
		private MenuItem menuItemTogglePatternWindow;
		private MenuItem menuItemToggleStatusWindow;
		private MacFace.FaceDef curFaceDef;

        private IOptimusMini optimusMini;

        [STAThread]
		public static void Main(string[] args)
		{
            //NtKernel.SYSTEM_BASIC_INFORMATION info = NtKernel.QuerySystemBasicInformation();
            //NtKernel.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION[] list =
            //    NtKernel.QuerySystemProcessorPerformanceInfomation((int)info.ActiveProcessors);
            //MachineStatus status = MachineStatus.LocalMachineStatus();
            //int c = status.ProcessorCount;
            //NtKernel.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION[] list = status.ProcessorPerformances();

            Application.EnableVisualStyles();
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
			MacFaceApp app = new MacFaceApp();
			app.StartApplication();
		}


		public MacFaceApp()
		{
			config = Configuration.GetInstance();
			config.Load();
			
			pageio_count = 0;

            cpuStats = new CPUStatisticsNtQuerySystemInformation(61);
            memStats = new MemoryStatisticsNtQuerySystemInformation(61);

            patternWindow = null;
            statusWindow = null;

            InitializeComponent();

            // x64 環境で 32bit な OptimusMini.dll を読み込もうとすると当然エラーとなるので何もしないクラスにしておく
		    optimusMini = (IntPtr.Size == 4 ? (IOptimusMini)new OptimusMini() : new OptimusMiniMock());
            optimusMini.DisplayOn();

            //OptimusMini.OnKeyDownCallbackDelegate oKD = new OptimusMini.OnKeyDownCallbackDelegate(OnKeyDownCallbackHandler);
            //OptimusMini.OnDeviceStateChangedCallbackDelegate oDSC = new OptimusMini.OnDeviceStateChangedCallbackDelegate(OnDeviceStateChangedCallbackHandler);
            //OptimusMini.RegisterEventHandler(oKD, oDSC);

            CountProcessorUsage(null, null);

            updateTimer = new System.Windows.Forms.Timer();
			updateTimer.Enabled = false;
			updateTimer.Interval = 1000;
			updateTimer.Tick += new EventHandler(this.CountProcessorUsage);
		}

        void OnKeyDownCallbackHandler(int key)
        {
            Console.Write("Keydown {0}\n", key);
        }

        void OnDeviceStateChangedCallbackHandler(int state)
        {
            //useOptimusMini = (state > 0);
        }

		void InitializeComponent() 
		{
			// コンテキストメニュー
			ContextMenu contextMenu = new System.Windows.Forms.ContextMenu();
			menuItemTogglePatternWindow = new System.Windows.Forms.MenuItem();
			menuItemToggleStatusWindow = new System.Windows.Forms.MenuItem();
			MenuItem menuItemConfigure = new System.Windows.Forms.MenuItem();
            MenuItem menuItemPatternList = new System.Windows.Forms.MenuItem();
            MenuItem menuItemExit = new System.Windows.Forms.MenuItem();
			MenuItem menuVersionInfo = new System.Windows.Forms.MenuItem();

			menuItemTogglePatternWindow.Text = MES_OPEN_PATTERN_WINDOW;
			menuItemTogglePatternWindow.Click +=new EventHandler(menuItemTogglePatternWindow_Click);

			menuItemToggleStatusWindow.Text = MES_OPEN_STATUS_WINDOW;
			menuItemToggleStatusWindow.Click +=new EventHandler(menuItemToggleStatusWindow_Click);

			menuItemConfigure.Text = "MacFace の設定(&O)...";
			menuItemConfigure.Click +=new EventHandler(menuItemConfigure_Click);

            menuItemPatternList.Text = "パターン一覧をデスクトップにダンプ";
            menuItemPatternList.Click += new EventHandler(menuItemPatternList_Click);

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
                    menuItemPatternList,
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
				string path = Path.Combine(Application.StartupPath, "default.mcface");

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

            updateTimer.Interval = 100 * config.UpdateSpeed;
            updateTimer.Start();

			Microsoft.Win32.SystemEvents.SessionEnding += new Microsoft.Win32.SessionEndingEventHandler(SystemEvents_SessionEnding);
			Application.ApplicationExit += new EventHandler(Application_ApplicationExit);
			Application.Run(this);
		}

		void Application_ApplicationExit(object sender, EventArgs e)
		{
			notifyIcon.Visible = false;
			config.Save();
            optimusMini.Dispose();
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
			
			curFaceDef = newFaceDef;

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

			pageio_count += memUsage.Pageout * (memUsage.Pagein + 1);
            //if (pageio_count > 0) pageio_count += memUsage.Pagein;
			pageio_count -= pageio_count / 50 + 1;
			if (pageio_count < 0) pageio_count = 0;

			int pattern = cpuUsage.Active / 10;
			pattern += memUsage.Pageout / 15;
			pattern += memUsage.Pagein / 30;
			if (pattern > 10) pattern = 10;

			FaceDef.PatternSuite suite = FaceDef.PatternSuite.Normal;

			UInt64 avilable = (UInt64)memStats.TotalVisibleMemorySize * 1024 - memUsage.Used;
            if (pageio_count > 100) 
			{
				suite = FaceDef.PatternSuite.MemoryInsufficient;
			}
            else if (avilable < 0) 
			{
				suite = FaceDef.PatternSuite.MemoryDecline;
			}

			int markers = FaceDef.MarkerNone;
			if (memUsage.Pagein > 0) markers += FaceDef.MarkerPageIn;
			if (memUsage.Pageout > 0) markers += FaceDef.MarkerPageOut;

			if (patternWindow != null) 
			{
				patternWindow.UpdatePattern(suite, pattern, markers);
			}

			if (statusWindow != null) 
			{
				statusWindow.UpdateGraph();
			}

            if (optimusMini.IsAlive && curFaceDef != null)
            {
                Bitmap image = new Bitmap(96, 96);
                Graphics g = Graphics.FromImage(image);
                g.FillRectangle(new SolidBrush(Color.White), 0, 0, image.Width, image.Height);
                curFaceDef.DrawPatternImage(g, suite, pattern, markers, 96F / 128F);
                g.Dispose();

                optimusMini.ShowPicture(1, image);
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

			//LoadFaceDefine(config.FaceDefPath);
			patternWindow.FaceDef = curFaceDef;

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

        private void menuItemPatternList_Click(object sender, EventArgs e)
        {
            MacFace.FaceDef faceDef = new MacFace.FaceDef(config.FaceDefPath);
            Bitmap image = new Bitmap(128*11, 128*3);
            Graphics g = Graphics.FromImage(image);
            Image patternImg;


            for (int suite = 0; suite <= 2; suite++)
            {
                for (int no = 0; no <= 10; no++)
                {
                    patternImg = faceDef.PatternImage((MacFace.FaceDef.PatternSuite)suite, no, 0);
                    g.DrawImage(patternImg, 128 * no, 128 * suite);
                }
            }
            
            g.Dispose();

            image.Save(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\facedump.png",
                        System.Drawing.Imaging.ImageFormat.Png);
        }

		private void menuItemConfigure_Click(object sender, EventArgs e)
		{
			ConfigurationForm configForm = new ConfigurationForm();
			configForm.ConfigChanged += new ConfigChangedEvent(configForm_ConfigChanged);
			configForm.Show();
		}

		private void configForm_ConfigChanged()
		{
            if (curFaceDef.Path != config.FaceDefPath)
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

            updateTimer.Interval = 100 * config.UpdateSpeed;
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

		/// <summary>
		/// ハンドルされていない例外をキャッチして、スタックトレースを保存してデバッグに役立てる
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Exception ex = e.ExceptionObject as Exception; // Exception 以外が飛んでくるのは超特殊な場合のみ。

			if (MessageBox.Show(
				String.Format("アプリケーションの実行中に予期しない重大なエラーが発生しました。\n\nエラー内容:\n{0}\n\nエラー情報をファイルに保存し、報告していただくことで不具合の解決に役立つ可能性があります。エラー情報をファイルに保存しますか?",
				((Exception)(e.ExceptionObject)).Message)
				, Application.ProductName
				, MessageBoxButtons.YesNo
				, MessageBoxIcon.Error
				, MessageBoxDefaultButton.Button1
				) == DialogResult.Yes)
			{
				using (SaveFileDialog saveFileDialog = new SaveFileDialog())
				{
					saveFileDialog.DefaultExt = "txt";
					saveFileDialog.Filter = "テキストファイル|*.txt";
					saveFileDialog.FileName = String.Format("macface4win_stacktrace_{0:yyyyMMdd_HHmmss}.txt", DateTime.Now);
					if (saveFileDialog.ShowDialog() == DialogResult.OK)
					{
						using (Stream stream = saveFileDialog.OpenFile())
						using (StreamWriter sw = new StreamWriter(stream))
						{
							Assembly asm = Assembly.GetExecutingAssembly();

							sw.WriteLine("発生時刻: {0}", DateTime.Now);
							sw.WriteLine();
							sw.WriteLine("MacFace for Windows:");
							sw.WriteLine("========================");
							sw.WriteLine("バージョン: {0}", ((ApplicationVersionStringAttribute)(asm.GetCustomAttributes(typeof(ApplicationVersionStringAttribute), true))[0]).Version);
							sw.WriteLine("アセンブリ: {0}", Assembly.GetExecutingAssembly().FullName);
							sw.WriteLine();
							sw.WriteLine("環境情報:");
							sw.WriteLine("========================");
							sw.WriteLine("オペレーティングシステム: {0}", Environment.OSVersion);
							sw.WriteLine("Microsoft .NET Framework: {0}", Environment.Version);
							sw.WriteLine();
							sw.WriteLine("ハンドルされていない例外: ");
							sw.WriteLine("=========================");
							sw.WriteLine(ex.ToString());
						}
					}
				}
				
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void SystemEvents_SessionEnding(object sender, Microsoft.Win32.SessionEndingEventArgs e)
		{
			config.Save();
		}
	}
}
