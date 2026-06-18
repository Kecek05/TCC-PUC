using LunaWolfStudiosEditor.ScriptableSheets.Shared;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.Scanning
{
	[Serializable]
	public class ScanSettings
	{
		[SerializeField]
		private ScanOption m_Option = ScanOption.Default;
		public ScanOption Option { get => m_Option; set => m_Option = value; }

		[SerializeField]
		private ScanPathOption m_PathOption = ScanPathOption.Default;
		public ScanPathOption PathOption { get => m_PathOption; set => m_PathOption = value; }

		[SerializeField]
		private string m_Path = UnityConstants.DefaultAssetPath;
		public string Path { get => m_Path; set => m_Path = value; }

		[SerializeField]
		private bool m_ShowProgressBar = true;
		public bool ShowProgressBar { get => m_ShowProgressBar; set => m_ShowProgressBar = value; }

		[SerializeField]
		private bool m_RootPrefabsOnly = true;
		public bool RootPrefabsOnly { get => m_RootPrefabsOnly; set => m_RootPrefabsOnly = value; }

		[SerializeField]
		private List<string> m_ExcludedPaths = new List<string>();
		public List<string> ExcludedPaths { get => m_ExcludedPaths; set => m_ExcludedPaths = value; }

		[SerializeField]
		private List<string> m_ExcludedFullTypeNames = new List<string>();
		public List<string> ExcludedFullTypeNames { get => m_ExcludedFullTypeNames; set => m_ExcludedFullTypeNames = value; }

		public string[] GetScanPaths()
		{
			switch (m_PathOption)
			{
				case ScanPathOption.Default:
					return new string[] { m_Path };
				case ScanPathOption.Assets:
					return new string[] { UnityConstants.DefaultAssetPath };
				case ScanPathOption.Packages:
					return new string[] { UnityConstants.Packages };
				case ScanPathOption.All:
					return new string[] { UnityConstants.DefaultAssetPath, UnityConstants.Packages };
				default:
					Debug.LogWarning($"Selected {nameof(ScanPathOption)} {m_PathOption} is not defined. Using default scan path {UnityConstants.DefaultAssetPath}.");
					return new string[] { UnityConstants.DefaultAssetPath };
			}
		}

		public string GetFirstScanPath()
		{
			return GetScanPaths()[0];
		}

		public string GetJoinedScanPaths()
		{
			var scanPaths = GetScanPaths();
			return string.Join("\n", scanPaths);
		}

		public string GetJoinedScanPaths(string[] scanPaths)
		{
			return string.Join("\n", scanPaths);
		}

		public bool IsPathExcluded(string assetPath)
		{
			if (ExcludedPaths == null || ExcludedPaths.Count <= 0)
			{
				return false;
			}

			ExcludedPaths.RemoveAll(e => string.IsNullOrWhiteSpace(e));
			foreach (var excludedPath in ExcludedPaths)
			{
				var normalizedExcludedPath = excludedPath.Replace("\\", "/");
				if (assetPath.StartsWith(normalizedExcludedPath, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		public bool IsFullTypeNameExcluded(Type type)
		{
			if (ExcludedFullTypeNames == null || ExcludedFullTypeNames.Count == 0)
			{
				return false;
			}

			ExcludedFullTypeNames.RemoveAll(e => string.IsNullOrWhiteSpace(e));
			foreach (var excluded in ExcludedFullTypeNames)
			{
				if (type.FullName.StartsWith(excluded, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}
	}
}
