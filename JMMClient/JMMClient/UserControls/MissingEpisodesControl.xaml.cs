﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Collections.ObjectModel;
using JMMClient.ViewModel;
using System.IO;
using System.Diagnostics;

namespace JMMClient.UserControls
{
	/// <summary>
	/// Interaction logic for MissingEpisodesControl.xaml
	/// </summary>
	public partial class MissingEpisodesControl : UserControl
	{
		BackgroundWorker workerFiles = new BackgroundWorker();

		public ICollectionView ViewEpisodes { get; set; }
		public ObservableCollection<MissingEpisodeVM> MissingEpisodesCollection { get; set; }

		private List<JMMServerBinary.Contract_MissingEpisode> contracts = new List<JMMServerBinary.Contract_MissingEpisode>();

		public static readonly DependencyProperty EpisodeCountProperty = DependencyProperty.Register("EpisodeCount",
			typeof(int), typeof(MissingEpisodesControl), new UIPropertyMetadata(0, null));

		public int EpisodeCount
		{
			get { return (int)GetValue(EpisodeCountProperty); }
			set { SetValue(EpisodeCountProperty, value); }
		}

		public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register("IsLoading",
			typeof(bool), typeof(MissingEpisodesControl), new UIPropertyMetadata(false, null));

		public bool IsLoading
		{
			get { return (bool)GetValue(IsLoadingProperty); }
			set
			{
				SetValue(IsLoadingProperty, value);
				IsNotLoading = !IsLoading;
			}
		}

		public static readonly DependencyProperty IsNotLoadingProperty = DependencyProperty.Register("IsNotLoading",
			typeof(bool), typeof(MissingEpisodesControl), new UIPropertyMetadata(true, null));

		public bool IsNotLoading
		{
			get { return (bool)GetValue(IsNotLoadingProperty); }
			set { SetValue(IsNotLoadingProperty, value); }
		}

		public static readonly DependencyProperty ReadyToExportProperty = DependencyProperty.Register("ReadyToExport",
			typeof(bool), typeof(MissingEpisodesControl), new UIPropertyMetadata(false, null));

		public static readonly DependencyProperty StatusMessageProperty = DependencyProperty.Register("StatusMessage",
			typeof(string), typeof(MissingEpisodesControl), new UIPropertyMetadata("", null));

		public string StatusMessage
		{
			get { return (string)GetValue(StatusMessageProperty); }
			set { SetValue(StatusMessageProperty, value); }
		}

		public bool ReadyToExport
		{
			get { return (bool)GetValue(ReadyToExportProperty); }
			set { SetValue(ReadyToExportProperty, value); }
		}

		public MissingEpisodesControl()
		{
			InitializeComponent();

			ReadyToExport = false;
			IsLoading = false;

			MissingEpisodesCollection = new ObservableCollection<MissingEpisodeVM>();
			ViewEpisodes = CollectionViewSource.GetDefaultView(MissingEpisodesCollection);
			ViewEpisodes.SortDescriptions.Add(new SortDescription("AnimeTitle", ListSortDirection.Ascending));
			ViewEpisodes.SortDescriptions.Add(new SortDescription("EpisodeType", ListSortDirection.Ascending));
			ViewEpisodes.SortDescriptions.Add(new SortDescription("EpisodeNumber", ListSortDirection.Ascending));

			btnRefresh.Click += new RoutedEventHandler(btnRefresh_Click);
			btnExport.Click += new RoutedEventHandler(btnExport_Click);

			workerFiles.DoWork += new DoWorkEventHandler(workerFiles_DoWork);
			workerFiles.RunWorkerCompleted += new RunWorkerCompletedEventHandler(workerFiles_RunWorkerCompleted);
		}

		void btnExport_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				IsLoading = true;
				btnRefresh.IsEnabled = false;
				btnExport.IsEnabled = false;
				ReadyToExport = false;
				this.Cursor = Cursors.Wait;

				StatusMessage = "Exporting...";

				string appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
				string logName = System.IO.Path.Combine(appPath, "AnimeEpisodes.txt");

				string export = "";
				foreach (MissingEpisodeVM missingEp in MissingEpisodesCollection)
				{
					export += string.Format("{0}({1}) - {2} *** {3} *** {4}", missingEp.AnimeTitle, missingEp.AnimeID,
						missingEp.EpisodeTypeAndNumber, missingEp.GroupFileSummary, missingEp.AniDB_SiteURL);
					export += Environment.NewLine;
				}

				StreamWriter Tex = new StreamWriter(logName);
				Tex.Write(export);
				Tex.Flush();
				Tex.Close();

				Process.Start(logName);
			}
			catch (Exception ex)
			{
				Utils.ShowErrorMessage(ex);
			}

			IsLoading = false;
			btnRefresh.IsEnabled = true;
			btnExport.IsEnabled = true;
			ReadyToExport = true;
			this.Cursor = Cursors.Arrow;

			StatusMessage = "";
		}

		void workerFiles_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			contracts = e.Result as List<JMMServerBinary.Contract_MissingEpisode>;
			foreach (JMMServerBinary.Contract_MissingEpisode mf in contracts)
				MissingEpisodesCollection.Add(new MissingEpisodeVM(mf));

			EpisodeCount = contracts.Count;
			ReadyToExport = EpisodeCount >= 1;
			btnRefresh.IsEnabled = true;
			IsLoading = false;
		}

		void workerFiles_DoWork(object sender, DoWorkEventArgs e)
		{
			try
			{
				WorkRequest wr = e.Argument as WorkRequest;
				List<JMMServerBinary.Contract_MissingEpisode> contractsTemp = JMMServerVM.Instance.clientBinaryHTTP.GetMissingEpisodes(
					JMMServerVM.Instance.CurrentUser.JMMUserID.Value, wr.MyGroupsOnly, wr.RegularEpisodesOnly);
				e.Result = contractsTemp;
			}
			catch (Exception ex)
			{
				Utils.ShowErrorMessage(ex);
			}
		}

		void btnRefresh_Click(object sender, RoutedEventArgs e)
		{
			IsLoading = true;
			btnRefresh.IsEnabled = false;
			MissingEpisodesCollection.Clear();
			ReadyToExport = false;
			EpisodeCount = 0;

			StatusMessage = "Loading...";
			WorkRequest wr = new WorkRequest(chkMyGroupsOnly.IsChecked.Value, chkRegularEpisodesOnly.IsChecked.Value);

			workerFiles.RunWorkerAsync(wr);
		}
	}

	class WorkRequest
	{
		public bool MyGroupsOnly { get; set; }
		public bool RegularEpisodesOnly { get; set; }

		public WorkRequest(bool myGroups, bool regEps)
		{
			MyGroupsOnly = myGroups;
			RegularEpisodesOnly = regEps;
		}
	}
}