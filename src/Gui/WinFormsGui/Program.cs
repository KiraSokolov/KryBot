﻿using System;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using Exceptionless;
using KryBot.CommonResources.Localization;
using KryBot.Gui.WinFormsGui.Forms;
using NLog;
using LogManager = KryBot.Core.LogManager;

namespace KryBot.Gui.WinFormsGui
{
    public static class Program
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        ///     Главная точка входа для приложения.
        /// </summary>
        [STAThread]
        public static void Main()
        {
            StartupInfo();
            Application.ThreadException += FormMain_UIThreadException;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            {
                ExceptionlessClient.Default.Register();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new FormMain());
            }
        }

        private static void StartupInfo()
        {
            try
            {
                Logger.Info("Startup application.");
            }
            catch (InvalidOperationException exc)
            {
                Logger.Error(exc, "Error processing assembly attributes.");
            }
            Logger.Info(
                CultureInfo.InvariantCulture,
                "ОС: {0}, {1}-bits. MS .NET Framework Runtime {2}.",
                Environment.OSVersion,
                Environment.Is64BitOperatingSystem ? "64" : "32",
                Environment.Version);
        }

        private static void FormMain_UIThreadException(object sender, ThreadExceptionEventArgs t)
        {
            MessageBox.Show($@"[{t.Exception.TargetSite}] {{{t.Exception.Message}}}", strings.Error,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Logger.Fatal(t.Exception);
            t.Exception.ToExceptionless().Submit();
            Environment.Exit(0);
        }
    }
}