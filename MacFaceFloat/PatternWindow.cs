/*
 * MacFace �p�^�[���E�C���h�E�N���X
 *
 * $Id$
 * 
 */

using System;
using System.Windows.Forms;
using System.Drawing;

namespace MacFace.FloatApp
{
	public class PatternWindow : Misuzilla.Windows.Forms.AlphaForm
	{
		private FaceDef curFaceDef;
		private float patternSize;

		private FaceDef.PatternSuite curSuite;
		private int curPattern;
		private int curMarkers;

		// �R���X�g���N�^
		public PatternWindow()
		{
			InitializeComponent();
			TransparentMouseMessage = false;
			MoveAtFormDrag = true;

			patternSize = 1.0F;
			curSuite   = FaceDef.PatternSuite.Normal;
			curPattern = 0;
			curMarkers = 0;
		}

		void InitializeComponent()
		{
			this.AutoScale = false;
			this.ClientSize = new System.Drawing.Size(128, 128);
			this.ControlBox = false;
			this.FormBorderStyle = FormBorderStyle.None;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "PatternWindow";
			this.Opacity = 0.75F;
			this.ShowInTaskbar = false;
			this.Text = "MacFace For Windows";
			this.TopMost = true;
		}

		public float PatternSize
		{
			get { return patternSize; }
			set {
				patternSize = value;
				Width = (int)Math.Ceiling(FaceDef.ImageWidth * patternSize);
				Height = (int)Math.Ceiling(FaceDef.ImageHeight * patternSize);
			}
		}

		public FaceDef FaceDef
		{
			get { return curFaceDef; }
			set { curFaceDef = value; }
		}

		public void UpdatePattern(FaceDef.PatternSuite suite, int patternNo, int markers)
		{
			if (curSuite != suite || curPattern != patternNo || curMarkers != markers) 
			{
				curSuite   = suite;
				curPattern = patternNo;
				curMarkers = markers;
				Refresh();
			}
		}

		public override void Refresh()
		{
			Graphics g = this.Graphics;
			g.Clear(Color.FromArgb(0, 0, 0, 0));
			curFaceDef.DrawPatternImage(g, curSuite, curPattern, curMarkers, patternSize);
			base.Refresh();
		}
	}
}