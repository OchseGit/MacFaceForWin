/*
 * MainForm.cs
 * $Id$
 * 
 * project created on 2004/06/02 at 2:43
 * 
 */

using System;
using System.Windows.Forms;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Diagnostics;
using System.Threading;

namespace MacFace.FloatApp
{
	public class MainForm : Misuzilla.Windows.Forms.AlphaForm
	{
		private System.Windows.Forms.ContextMenu contextMenu;
		private System.Windows.Forms.MenuItem menuItemPatternSelect;
		private System.Windows.Forms.MenuItem menuItemConfigure;
		private System.Windows.Forms.MenuItem menuItemExit;
		private String _facePath;
		private System.Windows.Forms.Timer _updateTimer;

		private FaceDef _currentFaceDef;
		private Configuration _config;
		
		Int32 prevUsage;
		PerformanceCounter cpuCount;
//		PerformanceCounter pageoutCount;
//		PerformanceCounter pageinCount;

		// �R���X�g���N�^
		public MainForm()
		{
			InitializeComponent();
			this.TransparentMouseMessage = false;
			this.MoveAtFormDrag = true;

			prevUsage = -10;

			cpuCount = new PerformanceCounter();
			cpuCount.CategoryName = "Processor";
			cpuCount.CounterName  = "% Processor Time";
			cpuCount.InstanceName = "_Total";

//			pageoutCount = new PerformanceCounter();
//			pageoutCount.CategoryName = "Memory";
//			pageoutCount.CounterName  = "Pages Output/sec";
//
//			pageinCount = new PerformanceCounter();
//			pageinCount.CategoryName = "Memory";
//			pageinCount.CounterName  = "Pages Input/sec";

			_updateTimer = new System.Windows.Forms.Timer();
			_updateTimer.Enabled = false;
			_updateTimer.Interval = 1000;
			_updateTimer.Tick += new EventHandler(this.CountProcessorUsage);
		}

		void InitializeComponent() {
			this.menuItemPatternSelect = new System.Windows.Forms.MenuItem();
			this.menuItemConfigure = new System.Windows.Forms.MenuItem();
			this.menuItemExit = new System.Windows.Forms.MenuItem();
			this.contextMenu = new System.Windows.Forms.ContextMenu();
			// 
			// menuItemPatternSelect
			// 
			this.menuItemPatternSelect.Text = "��p�^�[���̑I��(&S)";
			this.menuItemPatternSelect.Click += new System.EventHandler(this.menuItemPatternSelect_Click);

			// 
			// menuItemConfigure
			// 
			this.menuItemConfigure.Text = "MacFace �̐ݒ�(&C)...";
			this.menuItemConfigure.Click +=new EventHandler(menuItemConfigure_Click);
			// 
			// menuItemExit
			// 
			this.menuItemExit.Index = 0;
			this.menuItemExit.Shortcut = System.Windows.Forms.Shortcut.CtrlQ;
			this.menuItemExit.Text = "�I��(&X)";
			this.menuItemExit.Click += new System.EventHandler(this.menuItemExit_Click);
			// 
			// contextMenu
			// 
			this.contextMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
						this.menuItemPatternSelect, this.menuItemConfigure, new MenuItem("-"), this.menuItemExit});
			// 
			// MainForm
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 12);
			this.ClientSize = new System.Drawing.Size(120, 101);
			this.ContextMenu = this.contextMenu;
			this.ControlBox = false;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "MainForm";
			this.Opacity = 0.75F;
			this.ShowInTaskbar = false;
			this.Text = "MacFace For Windows";
			this.TopMost = true;
			this.Load += new EventHandler(MainForm_Load);
			this.Closing += new CancelEventHandler(MainForm_Closing);
			this.Move += new EventHandler(MainForm_Move);
		}
			

		[STAThread]
		public static void Main(string[] args)
		{
			Application.Run(new MainForm());
		}
		

		/*
		 * ��p�^�[����`�t�H���_�I���B
		 */
		public bool SelectFaceDefine()
		{
			return SelectFaceDefine(Application.StartupPath);
		}

		public bool SelectFaceDefine(string defaultPath)
		{
			while (true) 
			{
				FolderBrowserDialog folderBrowser = new FolderBrowserDialog();
				folderBrowser.SelectedPath = defaultPath;
				folderBrowser.Description = "��p�^�[���t�@�C���̑��݂���t�H���_��I�����Ă��������B";
				if (folderBrowser.ShowDialog() == DialogResult.OK) 
				{
					if (LoadFaceDefine(folderBrowser.SelectedPath)) 
					{
						return true;
					}
				}
				else 
				{
					return false;
				}
			}

		}


		public bool LoadFaceDefine(string path)
		{
			FaceDef newFaceDef = null;
			string plistPath = Path.Combine(path, "faceDef.plist");

			if (!File.Exists(plistPath))
			{
				System.Windows.Forms.MessageBox.Show(
					String.Format("�w�肳�ꂽ�t�H���_�Ɋ�p�^�[����`XML�t�@�C�� \"faceDef.plist\" �����݂��܂���B\n\n�t�H���_:\n{0}", path),
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
					String.Format("��p�^�[����`XML�t�@�C����ǂݍ��ލۂɃG���[���������܂����B\n\n����:\n{0}",
					ie.ToString()), "MacFace for Windows", MessageBoxButtons.OK, MessageBoxIcon.Error);
				
				return false;
			}
			catch (System.Xml.XmlException xe) 
			{
				System.Windows.Forms.MessageBox.Show(
					String.Format("��p�^�[����`XML�t�@�C����Ǎ��ݒ��ɃG���[���������܂����B\n\n����:\n{0}",
					xe.ToString()), "MacFace for Windows", MessageBoxButtons.OK, MessageBoxIcon.Error);
				
				return false;
			}

			// ��p�^�[�������ւ����͍X�V���~�߂Ă���
			if (_updateTimer != null) _updateTimer.Stop();

			_currentFaceDef = newFaceDef;
			_facePath = _currentFaceDef.Path;
			prevUsage = -10;

			// �X�V�ĊJ
			if (_updateTimer != null) _updateTimer.Start();

			return true;
		}

		public void CountProcessorUsage(object sender, EventArgs e)
		{
			Int32 usage = (Int32)cpuCount.NextValue();
//			Int32 pagein = (Int32)pageinCount.NextValue();
//			Int32 pageout = (Int32)pageoutCount.NextValue();

//				Console.WriteLine("Processor: {0}% (pattern: {1}) {2} {3}", usage, usage/10, pagein, pageout);
			if (usage >= 100) {
				usage = 100;
			} else if (usage < 0) {
				usage = 0;
			}
				
			if (prevUsage/10 != usage/10) {
				this.Graphics.Clear(Color.FromArgb(0, 0, 0, 0));
				foreach (Part part in _currentFaceDef.Pattern(FaceDef.PatternSuite.Normal, usage/10))
				{
					this.Graphics.DrawImage(part.Image,
						part.Point.X, part.Point.Y,
						part.Image.Size.Width, part.Image.Size.Height);
				}
				this.Update();
			}
				
			prevUsage = usage;			
		}
		

		//
		// �N��
		//
		public void MainForm_Load(object sender, System.EventArgs e)
		{
			// �ݒ�
			_config = Configuration.GetInstance();
			_config.Load();

			ApplyConfiguration();

			// ��p�^�[���ǂݍ���
			bool result = false;
			if (Directory.Exists(_config.FaceDefPath))
			{
				result = LoadFaceDefine(_config.FaceDefPath);
			}

			if (!result)
			{
				if (!SelectFaceDefine(Application.StartupPath))
				{
					Application.Exit();
					return;
				}

			}
		}

		// 
		// �I��
		//
		private void MainForm_Closing(object sender, CancelEventArgs e)
		{
			// �ۑ�
			_config.Opacity = (int) (this.Opacity * 100);
			_config.FaceDefPath = (_currentFaceDef != null ? _currentFaceDef.Path : Path.Combine(Application.StartupPath, "default.mcface"));
			_config.Location = this.Location;
			_config.TransparentMouseMessage = this.TransparentMouseMessage;

			_config.Save();
		}


		/*
		 * ���j���[�N���b�N�C�x���g
		 */
		public void menuItemPatternSelect_Click(object sender, System.EventArgs e)
		{
			SelectFaceDefine(_facePath);	
		}

		public void menuItemExit_Click(object sender, System.EventArgs e)
		{
			_updateTimer.Stop();
			this.Close();
		}

		private void menuItemConfigure_Click(object sender, EventArgs e)
		{
			ConfigurationForm configForm = new ConfigurationForm(this);
			if (configForm.ShowDialog() == DialogResult.OK) 
			{
				ApplyConfiguration();
			}
		}

		private void ApplyConfiguration()
		{
			this.Opacity = (float)_config.Opacity / 100;
			this.Location = _config.Location;
			this.TransparentMouseMessage = _config.TransparentMouseMessage;
		}

		private void MainForm_Move(object sender, EventArgs e)
		{
			_config.Location = this.Location;
		}
	}
}