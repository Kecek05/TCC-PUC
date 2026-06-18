using LunaWolfStudiosEditor.ScriptableSheets.Layout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using static UnityEditor.GenericMenu;

namespace LunaWolfStudiosEditor.ScriptableSheets
{
	public class SheetsMultiColumnHeader : MultiColumnHeader
	{
		private static readonly FieldInfo s_ColumnRectsField = typeof(MultiColumnHeader).GetField("m_ColumnRects", BindingFlags.Instance | BindingFlags.NonPublic);

		private readonly MenuFunction2 m_CopyColumnFunc;
		private readonly MenuFunction2 m_SearchFilterFunc;

		private int m_ContextColumn;

		private HashSet<int> m_DockedColumns = new HashSet<int>();
		public HashSet<int> DockedColumns { get => m_DockedColumns; set => m_DockedColumns = value; }

		public event HeaderCallback dockedColumnsChanged;

		private float m_ScrollX;
		private float m_WidthOfAllVisibleDockedColumns;

		public SheetsMultiColumnHeader(MultiColumnHeaderState state, MenuFunction2 copyColumnFunc, MenuFunction2 searchFilterFunc) : base(state)
		{
			m_CopyColumnFunc = copyColumnFunc;
			m_SearchFilterFunc = searchFilterFunc;
		}

		public override void OnGUI(Rect rect, float xScroll)
		{
			// We want to draw the ColumnHeader as a single line, but each row will be the row height.
			var totalHeaderRect = new Rect(0f, 0f, rect.width, rect.height);
			rect.height = EditorGUIUtility.singleLineHeight;

			base.OnGUI(rect, xScroll);

			if (DockedColumns.Count > 0)
			{
				UpdateColumnHeaderRectsWithDocking(totalHeaderRect, xScroll);
			}
			else if (totalHeaderRect.height > rect.height)
			{
				UpdateColumnHeaderRectHeight(totalHeaderRect);
			}
		}

		private void UpdateColumnHeaderRectHeight(Rect totalHeaderRect)
		{
			var columnRects = (Rect[]) s_ColumnRectsField.GetValue(this);

			for (var i = 0; i < columnRects.Length; i++)
			{
				columnRects[i].height = totalHeaderRect.height;
			}

			s_ColumnRectsField.SetValue(this, columnRects);
		}

		private void UpdateColumnHeaderRectsWithDocking(Rect totalHeaderRect, float xScroll)
		{
			var columnRects = (Rect[]) s_ColumnRectsField.GetValue(this);

			var rect = totalHeaderRect;
			var dockedRect = new Rect();
			for (var i = 0; i < state.visibleColumns.Length; i++)
			{
				var num = state.visibleColumns[i];
				var column = state.columns[num];
				if (i > 0)
				{
					rect.x += rect.width;
				}

				rect.width = column.width;
				columnRects[i] = rect;

				// Name and Actions column move with scroll.
				if (num <= 1 && DockedColumns.Contains(num))
				{
					// Logic for the first docked column differs from the next.
					if (dockedRect == Rect.zero)
					{
						columnRects[i].x = Mathf.Max(columnRects[i].x, columnRects[0].x + xScroll);
						dockedRect = columnRects[i];
					}
					else
					{
						columnRects[i].x += xScroll;
						dockedRect.xMax = columnRects[i].xMax;
					}
				}
				else if (dockedRect.xMax > columnRects[i].x)
				{
					var columRectXMax = columnRects[i].xMax;
					columnRects[i].x = dockedRect.xMax;
					columnRects[i].width = Mathf.Max(0, columRectXMax - columnRects[i].x);
				}
			}

			s_ColumnRectsField.SetValue(this, columnRects);

			m_ScrollX = xScroll;
			// Store this once so we don't have to repeatedly call it for the ColumnHeaderGUI.
			m_WidthOfAllVisibleDockedColumns = GetWidthOfAllVisibleDockedColumns();
		}

		protected override void ColumnHeaderGUI(MultiColumnHeaderState.Column column, Rect headerRect, int columnIndex)
		{
			headerRect.height = EditorGUIUtility.singleLineHeight;

			var isDocked = false;
			if (DockedColumns.Count > 0)
			{
				if (DockedColumns.Contains(columnIndex))
				{
					isDocked = true;
					// If Actions is docked then we just add the scroll X to all docked columns.
					if (DockedColumns.Contains(0))
					{
						headerRect.x += m_ScrollX;
					}
					else
					{
						headerRect.x = Mathf.Max(headerRect.x, GetColumnRect(0).x + m_ScrollX);
					}
					EditorGUI.DrawRect(headerRect, SheetLayout.DockedHeaderBackgroundColor);
				}
				// Don't hide the Actions column label.
				else if (columnIndex > 0 && m_WidthOfAllVisibleDockedColumns > 0)
				{
					var xOffset = m_WidthOfAllVisibleDockedColumns + m_ScrollX;
					if (headerRect.x < xOffset)
					{
						var delta = xOffset - headerRect.x;
						headerRect.x += delta;
						headerRect.xMax -= delta;
						if (headerRect.width < SheetLayout.MinColumnHeaderWidth)
						{
							return;
						}
					}
				}
			}

			SortingButton(column, headerRect, columnIndex);

			var style = GetStyle(column.headerTextAlignment);
			if (isDocked)
			{
				style = new GUIStyle(style)
				{
					normal = { textColor = SheetLayout.DockedHeaderLabelColor }
				};
			}

			var num = SheetLayout.kWindowToolbarHeight;
			var position = new Rect(headerRect.x, headerRect.yMax - num, headerRect.width, num);
			GUI.Label(position, column.headerContent, style);

			var e = Event.current;
			if (e.type == EventType.MouseDown && e.button == 1 && headerRect.Contains(e.mousePosition))
			{
				m_ContextColumn = columnIndex;
			}
		}

		protected override void AddColumnHeaderContextMenuItems(GenericMenu menu)
		{
			var contextColumn = state.columns[m_ContextColumn];
			var contextColumnName = contextColumn.headerContent.text;
			menu.AddDisabledItem(new GUIContent($"[{contextColumnName}]"));

			// Can only Dock Actions and Name column.
			if (m_ContextColumn <= 1)
			{
				menu.AddItem(EditorGUIUtility.TrTextContent("Dock column"), DockedColumns.Contains(m_ContextColumn), ToggleColumnDocking, m_ContextColumn);
			}

			if (m_ContextColumn > 0)
			{
				menu.AddItem(EditorGUIUtility.TrTextContent("Hide column"), false, ToggleColumnVisibility, m_ContextColumn);
				menu.AddItem(EditorGUIUtility.TrTextContent("Copy column"), false, m_CopyColumnFunc, m_ContextColumn);
				menu.AddItem(EditorGUIUtility.TrTextContent("Copy property path"), false, CopyPropertyPath, m_ContextColumn);
				menu.AddItem(EditorGUIUtility.TrTextContent("Filter by property"), false, m_SearchFilterFunc, m_ContextColumn);
				menu.AddSeparator(string.Empty);
			}

			menu.AddItem(EditorGUIUtility.TrTextContent("Resize to Fit"), false, ResizeToFit);
			menu.AddSeparator(string.Empty);

			var columnNameCounters = new Dictionary<string, int>();
			for (int i = 0; i < state.columns.Length && i < SheetLayout.MenuItemLimit; i++)
			{
				var column = state.columns[i];
				var columnName = column.headerContent.text;
				var menuItemName = columnName;

				if (columnNameCounters.ContainsKey(columnName))
				{
					columnNameCounters[columnName]++;
					menuItemName = $"{columnName} ({columnNameCounters[columnName]})";
				}
				else
				{
					columnNameCounters[columnName] = 0;
				}

				if (column.allowToggleVisibility)
				{
					menu.AddItem(new GUIContent(menuItemName), state.visibleColumns.Contains(i), ToggleColumnVisibility, i);
				}
				else
				{
					menu.AddDisabledItem(new GUIContent(menuItemName));
				}
			}

			m_ContextColumn = 0;
		}

		public float GetWidthOfAllVisibleDockedColumns()
		{
			return state.visibleColumns.Where(v => m_DockedColumns.Contains(v)).Sum(d => state.columns[d].width);
		}

		protected virtual void OnDockedColumnsChanged()
		{
			dockedColumnsChanged?.Invoke(this);
		}

		private GUIStyle GetStyle(TextAlignment alignment)
		{
			switch (alignment)
			{
				case TextAlignment.Left:
					return DefaultStyles.columnHeader;
				case TextAlignment.Center:
					return DefaultStyles.columnHeaderCenterAligned;
				case TextAlignment.Right:
					return DefaultStyles.columnHeaderRightAligned;
				default:
					return DefaultStyles.columnHeader;
			}
		}

		private void ToggleColumnVisibility(object columnData)
		{
			ToggleVisibility((int) columnData);
		}

		private void ToggleColumnDocking(object columnData)
		{
			var columnIndex = (int) columnData;
			if (DockedColumns.Contains(columnIndex))
			{
				DockedColumns.Remove(columnIndex);
			}
			else
			{
				DockedColumns.Add(columnIndex);
			}
			OnDockedColumnsChanged();
		}

		private void CopyPropertyPath(object columnData)
		{
			EditorGUIUtility.systemCopyBuffer = state.columns[(int) columnData].headerContent.tooltip;
		}
	}
}