﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using PowerToys.FileLocksmithUI.Properties;
using Windows.Graphics;
using WinUIEx;

namespace FileLocksmithUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WindowEx, IDisposable
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            StartFindingProcesses();
        }

        private void StartFindingProcesses()
        {
            Thread thread = new Thread(FindProcesses);
            thread.Start();
        }

        private void FindProcesses()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // Mock
                stackPanel.Children.Add(new ProcessEntry("WindowsTerminal.exe", 123456, 1));
            });
        }

        public void Dispose()
        {
        }
    }
}