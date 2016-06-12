﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using KryBot.Core;
using KryBot.Gui.WinFormsGui.Properties;

namespace KryBot.Gui.WinFormsGui.Forms
{
	public partial class FormLog : Form
	{
		readonly LogMessage _messages = LogMessage.Instance;
		private readonly int _y;
		private bool _win7;
		private int _x;

		public FormLog(int x, int y)
		{
			_x = x;
			_y = y;
			InitializeComponent();
			_messages.HandleMessage += OnHandleMessage;
		}

		private void formLog_Load(object sender, EventArgs e)
		{
			if (Environment.OSVersion.Version.ToString().Split('.')[1] == "1")
			{
				_win7 = true;
			}

			if (_win7)
			{
				_x = _x + 15;
			}

			Location = new Point(_x, _y);
			var owner = Owner as FormMain;
			if (owner != null) AppendText(richTextBox1, owner.LogBuffer.Content, owner.LogBuffer.Color);
			Design();
		}

		public void FormHide()
		{
			Hide();
		}

		public void FormUnHide()
		{
			Show();
		}

		public void FormChangeLocation()
		{
			var owner = Owner as FormMain;
			if (_win7)
			{
				if (owner != null) Location = new Point(owner.Location.X + owner.Width, owner.Location.Y);
			}
			else
			{
				if (owner != null) Location = new Point(owner.Location.X + owner.Width - 15, owner.Location.Y);
			}
		}

		private void Design()
		{
			Text = @"Лог";
			Icon = Icon.FromHandle(Resources.log.GetHicon());
			Height = Properties.Settings.Default.LogHeight;
			Width = Properties.Settings.Default.LogWidth;
		}

		private static void AppendText(RichTextBox box, string text, Color color)
		{
			box.SelectionStart = box.TextLength;
			box.SelectionLength = 0;

			box.SelectionColor = color;
			box.AppendText(text);
			box.SelectionColor = box.ForeColor;
		}

		private void richTextBox1_TextChanged(object sender, EventArgs e)
		{
			richTextBox1.SelectionStart = richTextBox1.Text.Length;
			richTextBox1.ScrollToCaret();
		}

		private void richTextBox1_LinkClicked(object sender, LinkClickedEventArgs e)
		{
			Process.Start(e.LinkText);
		}

		private void FormLog_ResizeEnd(object sender, EventArgs e)
		{
			Properties.Settings.Default.LogHeight = Height;
			Properties.Settings.Default.LogWidth = Width;
			Properties.Settings.Default.Save();
		}

		private void OnHandleMessage(object sender, EventArgs args)
		{
			var messageEvent = args as MessageEventArgs;
			if(messageEvent != null)
			{
				AppendText(richTextBox1, messageEvent.Message, messageEvent.Color);
			}
		}
	}
}