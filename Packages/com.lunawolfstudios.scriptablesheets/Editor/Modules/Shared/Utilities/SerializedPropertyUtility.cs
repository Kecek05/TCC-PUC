using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LunaWolfStudiosEditor.ScriptableSheets.Shared
{
	public static class SerializedPropertyUtility
	{
		public static readonly Object[] UnityBuiltInAssets;

		private static readonly List<SerializedPropertyType> s_InputFieldPropertyTypes = new List<SerializedPropertyType>()
		{
			SerializedPropertyType.Integer,
			SerializedPropertyType.Float,
			SerializedPropertyType.String,
			SerializedPropertyType.ArraySize,
			SerializedPropertyType.Character,
		};

		// Some Object fields are marked as editable when they shouldn't be. This list helps catch those.
		private static readonly List<string> s_ReadOnlyUnityFields = new List<string>() { UnityConstants.Field.Script };

		// Unity has a duplicate StaticEditorFlags value for ContributeGI and LightmapStatic so we remove one of them.
		private static readonly string[] s_StaticEditorFlags = Enum.GetNames(typeof(StaticEditorFlags)).Where(name => name != "LightmapStatic").ToArray();
		private static Type s_StringTableCollectionType;

		private static readonly GUIContent s_CreateButtonContent = EditorGUIUtility.IconContent("Toolbar Plus");
		private static readonly GUIContent s_ArraySizePlusButtonContent = EditorGUIUtility.IconContent("Toolbar Plus");
		private static readonly GUIContent s_ArraySizeMinusButtonContent = (EditorGUIUtility.IconContent("Toolbar Minus"));

		private const float MinArraySizeFieldWidth = 10f;
		private const float InlineButtonWidth = 20f;
		private const float DoubleButtonWidth = InlineButtonWidth * 2f;

		static SerializedPropertyUtility()
		{
			UnityBuiltInAssets = AssetDatabase.LoadAllAssetsAtPath(UnityConstants.Path.BuiltInExtra);
		}

		// Draw our own fields to eliminate unwanted header and text attributes that property field uses by default.
		public static void DrawProperty(this SerializedProperty property, Rect propertyRect, Object rootObject, bool isCustomField, out bool arraySizeChanged, bool showReadOnly, AssetPreviewSettings previewSettings, string defaultNewAssetPath, Action<ScriptableObject, Type> onObjectCreated, Action onResetTextEditing)
		{
			arraySizeChanged = false;
			// There are some fields on other Unity Asset types that are inaccessible. So only draw our own custom fields.
			if (isCustomField)
			{
				var propertyType = property.propertyType;
				switch (propertyType)
				{
					case SerializedPropertyType.Integer:
						propertyRect.height = EditorGUIUtility.singleLineHeight;
						if (property.IsTypeInt())
						{
							property.intValue = EditorGUI.IntField(propertyRect, property.intValue);
						}
						else if (property.IsTypeLong())
						{
							if (!showReadOnly && property.propertyPath.EndsWith(UnityConstants.Field.TableEntryReferenceKeyId))
							{
								DrawTableEntryKeyDropdown(propertyRect, property);
							}
							else
							{
								property.longValue = EditorGUI.LongField(propertyRect, property.longValue);
							}
						}
#if UNITY_2022_1_OR_NEWER
						else if (property.IsTypeUInt())
						{
							property.uintValue = DrawUtility.GUI.UIntField(propertyRect, property.uintValue);
						}
						else if (property.IsTypeULong())
						{
							property.ulongValue = DrawUtility.GUI.ULongField(propertyRect, property.ulongValue);
						}
#endif
						else
						{
							property.intValue = EditorGUI.IntField(propertyRect, property.intValue);
						}
						break;

					case SerializedPropertyType.ArraySize:
						var originalHeight = propertyRect.height;
						propertyRect.height = EditorGUIUtility.singleLineHeight;

						Rect fieldRect;
						Rect sizePlusRect;
						Rect sizeMinusRect;

						if (originalHeight > EditorGUIUtility.singleLineHeight)
						{
							// Vertical layout for +/- buttons when line height is greater than 1.
							var buttonRect = new Rect(propertyRect.xMax - InlineButtonWidth, propertyRect.y, InlineButtonWidth, originalHeight);
							fieldRect = new Rect(propertyRect.x, propertyRect.y, propertyRect.width - InlineButtonWidth, propertyRect.height);
							sizePlusRect = new Rect(buttonRect.x, buttonRect.y, buttonRect.width, EditorGUIUtility.singleLineHeight);
							sizeMinusRect = new Rect(buttonRect.x, buttonRect.y + EditorGUIUtility.singleLineHeight, buttonRect.width, EditorGUIUtility.singleLineHeight);
						}
						else
						{
							// Horizontal layout for +/- buttons when line height is 1.
							var buttonRect = new Rect(propertyRect.xMax - DoubleButtonWidth, propertyRect.y, DoubleButtonWidth, propertyRect.height);
							fieldRect = new Rect(propertyRect.x, propertyRect.y, propertyRect.width - DoubleButtonWidth, propertyRect.height);
							sizePlusRect = new Rect(buttonRect.x, buttonRect.y, InlineButtonWidth, buttonRect.height);
							sizeMinusRect = new Rect(buttonRect.x + InlineButtonWidth, buttonRect.y, InlineButtonWidth, buttonRect.height);
						}

						var previousValue = property.intValue;

						var showArraySizeButtons = fieldRect.width > MinArraySizeFieldWidth;
						if (!showArraySizeButtons)
						{
							fieldRect.width = propertyRect.width;
						}

						property.intValue = EditorGUI.IntField(fieldRect, property.intValue);

						if (showArraySizeButtons)
						{
							s_ArraySizePlusButtonContent.tooltip = "Add";
							if (GUI.Button(sizePlusRect, s_ArraySizePlusButtonContent))
							{
								onResetTextEditing?.Invoke();
								property.intValue++;
							}

							s_ArraySizeMinusButtonContent.tooltip = "Remove";
							if (GUI.Button(sizeMinusRect, s_ArraySizeMinusButtonContent))
							{
								onResetTextEditing?.Invoke();
								property.intValue = Mathf.Max(0, property.intValue - 1);
							}
						}

						if (previousValue != property.intValue)
						{
							arraySizeChanged = true;
						}
						break;

					case SerializedPropertyType.Boolean:
						propertyRect.height = EditorGUIUtility.singleLineHeight;
						property.boolValue = DrawUtility.GUI.ToggleCenter(propertyRect, property.boolValue);
						break;

					case SerializedPropertyType.Float:
						propertyRect.height = EditorGUIUtility.singleLineHeight;
						if (property.IsTypeFloat())
						{
							property.floatValue = EditorGUI.FloatField(propertyRect, property.floatValue);
						}
						else if (property.IsTypeDouble())
						{
							property.doubleValue = EditorGUI.DoubleField(propertyRect, property.floatValue);
						}
						else
						{
							property.floatValue = EditorGUI.FloatField(propertyRect, property.floatValue);
						}
						break;

					case SerializedPropertyType.String:
						if (!showReadOnly && property.propertyPath.EndsWith(UnityConstants.Field.TableReferenceCollectionName))
						{
							DrawLocalizationTablePicker(propertyRect, property, previewSettings);
						}
						else
						{
							property.stringValue = EditorGUI.TextField(propertyRect, property.stringValue);
						}
						break;

					case SerializedPropertyType.Color:
						property.colorValue = EditorGUI.ColorField(propertyRect, GUIContent.none, property.colorValue, true, true, false);
						break;

					case SerializedPropertyType.ObjectReference:
						// Only draw asset previews if the height is greater than 1.
						if (propertyRect.height > EditorGUIUtility.singleLineHeight)
						{
							DrawUtility.TableAssetPreview(property.objectReferenceValue, propertyRect, previewSettings);
							propertyRect.height = EditorGUIUtility.singleLineHeight;
						}
						if (!IsDefaultUnityEngineType(rootObject))
						{
							var objectType = ReflectionUtility.GetNestedFieldType(rootObject.GetType(), property.propertyPath);
							if (objectType != null)
							{
								// If the Object is null and it's a ScriptableObject then give the option to create a new instance of it directly from here.
								if (property.objectReferenceValue == null && typeof(ScriptableObject).IsAssignableFrom(objectType))
								{
									var objectRect = new Rect(propertyRect.x, propertyRect.y, propertyRect.width - InlineButtonWidth, propertyRect.height);
									var plusRect = new Rect(objectRect.xMax, propertyRect.y, InlineButtonWidth, propertyRect.height);
									property.objectReferenceValue = EditorGUI.ObjectField(objectRect, property.objectReferenceValue, objectType, false);
									s_CreateButtonContent.tooltip = $"Create new {objectType.Name} and auto-assign";
									if (GUI.Button(plusRect, s_CreateButtonContent))
									{
										var newObject = ScriptableObject.CreateInstance(objectType);
										var guids = AssetDatabase.FindAssets($"t:{objectType.Name}");
										var folderPath = defaultNewAssetPath;
										// First check if any existing asset path exists.
										if (guids != null && guids.Length > 0)
										{
											var fullPath = AssetDatabase.GUIDToAssetPath(guids[0]);
											var newFolderPath = System.IO.Path.GetDirectoryName(fullPath);
											// If they exist in a valid folder than use that. Otherwise use the default new asset path.
											if (AssetDatabase.IsValidFolder(newFolderPath))
											{
												folderPath = newFolderPath;
											}
										}
										var filename = $"New{objectType.Name}{UnityConstants.Extensions.Asset}";
										var uniquePath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{filename}");
										AssetDatabase.CreateAsset(newObject, uniquePath);
										AssetDatabase.SaveAssets();
										EditorGUIUtility.PingObject(newObject);
										Selection.activeObject = newObject;
										property.objectReferenceValue = newObject;
										onObjectCreated?.Invoke(newObject, objectType);
									}
								}
								else
								{
									property.objectReferenceValue = EditorGUI.ObjectField(propertyRect, property.objectReferenceValue, objectType, false);
								}
							}
							else
							{
								EditorGUI.PropertyField(propertyRect, property, GUIContent.none, false);
							}
						}
						else
						{
							EditorGUI.PropertyField(propertyRect, property, GUIContent.none, false);
						}
						break;

					case SerializedPropertyType.LayerMask:
						propertyRect.height = EditorGUIUtility.singleLineHeight;
						property.intValue = EditorGUI.MaskField(propertyRect, GUIContent.none, property.intValue, InternalEditorUtility.layers);
						break;

					case SerializedPropertyType.Enum:
						propertyRect.height = EditorGUIUtility.singleLineHeight;
						if (!IsDefaultUnityEngineType(rootObject, out string fullTypeName) && (!fullTypeName.StartsWith(UnityConstants.Type.TMPro) || IsAlignmentProperty(property.propertyPath)) && property.TryGetEnumType(rootObject, out Type enumType))
						{
							if (enumType.HasFlagsAttribute())
							{
								// Remove none and bitwise combination values.
								var filteredEnumNames = new List<string>();
								foreach (var value in Enum.GetValues(enumType))
								{
									var intValue = (int) value;
									if (intValue != 0 && (intValue & (intValue - 1)) == 0)
									{
										filteredEnumNames.Add(value.ToString());
									}
								}
								property.intValue = EditorGUI.MaskField(propertyRect, GUIContent.none, property.intValue, filteredEnumNames.ToArray());
							}
							else
							{
								property.intValue = Convert.ToInt32(EditorGUI.EnumPopup(propertyRect, (Enum) Enum.ToObject(enumType, property.intValue)));
							}
						}
						else
						{
							// Special case for UnityEngine.UI.Slider Component to remove extra space when rendering the Direction property.
							if (fullTypeName == UnityConstants.Type.UnityEngineUISlider && property.propertyPath == UnityConstants.Field.Direction)
							{
								propertyRect.y -= 8;
							}
							EditorGUI.PropertyField(propertyRect, property, GUIContent.none, false);
						}
						break;

					case SerializedPropertyType.Character:
						propertyRect.height = EditorGUIUtility.singleLineHeight;
						var stringValue = EditorGUI.TextField(propertyRect, ((char) property.intValue).ToString());
						if (!string.IsNullOrEmpty(stringValue))
						{
							property.intValue = stringValue[0];
						}
						break;

					case SerializedPropertyType.AnimationCurve:
						property.animationCurveValue = EditorGUI.CurveField(propertyRect, property.animationCurveValue);
						break;

					case SerializedPropertyType.Gradient:
						var internalPropertyHeight = EditorGUI.GetPropertyHeight(property, false);
						if (internalPropertyHeight > EditorGUIUtility.singleLineHeight)
						{
							var gradientValue = property.GetGradientValue();
							property.SetGradientValue(EditorGUI.GradientField(propertyRect, gradientValue));
						}
						else
						{
							// Workaround for Unity bug where EditorGUI.GradientField bleeds outside of its rect.
							EditorGUI.PropertyField(propertyRect, property, GUIContent.none, false);
						}
						break;

					case SerializedPropertyType.Generic:
					default:
						propertyRect.height = EditorGUIUtility.singleLineHeight;
						EditorGUI.PropertyField(propertyRect, property, GUIContent.none, false);
						break;
				}
			}
			else
			{
				// Draw Unity layer and tag fields accordingly.
				switch (property.propertyPath)
				{
					case UnityConstants.Field.Layer:
						propertyRect.height = EditorGUIUtility.singleLineHeight;
						property.intValue = EditorGUI.LayerField(propertyRect, property.intValue);
						break;

					case UnityConstants.Field.StaticEditorFlags:
						propertyRect.height = EditorGUIUtility.singleLineHeight;
						var newValue = EditorGUI.MaskField(propertyRect, property.intValue, s_StaticEditorFlags);
						// Unity uses -1 to represent everything in a mask field, but StaticEditorFlags uses int.MaxValue to represent everything.
						if (newValue <= -1)
						{
							newValue = int.MaxValue;
						}
						property.intValue = newValue;
						break;

					case UnityConstants.Field.Tag:
						propertyRect.height = EditorGUIUtility.singleLineHeight;
						property.stringValue = EditorGUI.TagField(propertyRect, property.stringValue);
						break;

					default:
						if (property.propertyType == SerializedPropertyType.Boolean)
						{
							propertyRect.height = EditorGUIUtility.singleLineHeight;
							property.boolValue = DrawUtility.GUI.ToggleCenter(propertyRect, property.boolValue);
						}
						else if (property.propertyType == SerializedPropertyType.String)
						{
							propertyRect.height = EditorGUIUtility.singleLineHeight;
							property.stringValue = EditorGUI.TextField(propertyRect, property.stringValue);
						}
						else if (property.propertyType == SerializedPropertyType.ObjectReference)
						{
							// Only draw asset previews if the height is greater than 1.
							if (propertyRect.height > EditorGUIUtility.singleLineHeight)
							{
								DrawUtility.TableAssetPreview(property.objectReferenceValue, propertyRect, previewSettings);
								propertyRect.height = EditorGUIUtility.singleLineHeight;
							}
							EditorGUI.PropertyField(propertyRect, property, GUIContent.none, false);
						}
						else
						{
							propertyRect.height = EditorGUIUtility.singleLineHeight;
							EditorGUI.PropertyField(propertyRect, property, GUIContent.none, false);
						}
						break;
				}
			}
		}

		public static SerializedPropertyType GetSheetsPropertyType(this SerializedProperty property, bool isScriptableObject)
		{
			if (isScriptableObject)
			{
				return property.propertyType;
			}
			// Handle special cases like Layers and Tags where Unity considers them int and string fields respectively.
			switch (property.propertyPath)
			{
				case UnityConstants.Field.Layer:
					return SerializedPropertyType.LayerMask;

				case UnityConstants.Field.StaticEditorFlags:
				case UnityConstants.Field.Tag:
					return SerializedPropertyType.Enum;

				default:
					return property.propertyType;
			}
		}

		public static string FriendlyPropertyPath(this SerializedProperty property)
		{
			return FriendlyPropertyPath(property.propertyPath, property.displayName);
		}

		public static string FriendlyPropertyPath(string propertyPath, string displayName)
		{
			var friendlyPath = propertyPath;
			if (friendlyPath.Contains('.'))
			{
				friendlyPath = friendlyPath.Replace(UnityConstants.ArrayPropertyPath, "[");
				friendlyPath = friendlyPath.Replace($".{UnityConstants.Field.Array}.{UnityConstants.Field.ArraySize}", $".{UnityConstants.Field.ArraySize}");
				friendlyPath = friendlyPath.Replace("m_", string.Empty);
				if (friendlyPath.Length > 1)
				{
					var lastIndex = friendlyPath.LastIndexOf('.');
					if (lastIndex > 0)
					{
						var secondLastIndex = friendlyPath.LastIndexOf('.', lastIndex - 1);
						if (secondLastIndex > -1)
						{
							friendlyPath = friendlyPath.Substring(secondLastIndex + 1);
						}
						else if (friendlyPath.Contains(']') && !friendlyPath.Contains("]."))
						{
							var lastSegment = friendlyPath.Substring(lastIndex + 1);
							friendlyPath = lastSegment;
						}
					}
					friendlyPath = friendlyPath.Replace('.', ' ').Trim();
					friendlyPath = Regex.Replace(friendlyPath, @"<([^>]+)>k__BackingField", "$1");
					friendlyPath = Regex.Replace(friendlyPath, @"(\p{Ll})(\p{Lu})", "$1 $2");
					friendlyPath = Regex.Replace(friendlyPath, @"\b[a-z]", c => c.Value.ToUpper());
					if (!string.IsNullOrEmpty(friendlyPath))
					{
						return friendlyPath;
					}
				}
			}
			return displayName;
		}

		// Needs to handle edge cases like:
		//  Sprite PPtr<$Sprite>
		//  PPtr<Material PPtr<Material>
		public static string FriendlyType(string type)
		{
			var lastOpen = type.LastIndexOf('<');
			var lastClose = type.LastIndexOf('>');
			if (lastOpen != -1 && lastClose != -1 && lastOpen < lastClose)
			{
				var lastValue = type.Substring(lastOpen + 1, lastClose - lastOpen - 1);
				return lastValue.Replace("$", string.Empty);
			}
			return type;
		}

		public static string FriendlyType(this SerializedProperty property)
		{
			return FriendlyType(property.type);
		}

		public static string GetFloatStringValue(this SerializedProperty property)
		{
			// We can't use SerializedProperty.numericType until 2022.1 so we can roll our own by checking type.
			switch (property.type)
			{
				case UnityConstants.Type.Float:
					return property.floatValue.ToString();

				case UnityConstants.Type.Double:
					return property.doubleValue.ToString();

				default:
					return property.floatValue.ToString();
			}
		}

		public static string GetIntStringValue(this SerializedProperty property)
		{
			switch (property.type)
			{
				case UnityConstants.Type.Int:
					return property.intValue.ToString();

				case UnityConstants.Type.Long:
					return property.longValue.ToString();

				case UnityConstants.Type.UInt:
#if UNITY_2022_1_OR_NEWER
					return property.uintValue.ToString();
#else
					return property.intValue.ToString();
#endif
				case UnityConstants.Type.ULong:
#if UNITY_2022_1_OR_NEWER
					return property.ulongValue.ToString();
#else
					return property.longValue.ToString();
#endif
				default:
					return property.intValue.ToString();
			}
		}

		public static bool TrySetFloatValue(this SerializedProperty property, string value)
		{
			var isPercentage = TryStripPercentage(value, out var number);
			switch (property.type)
			{
				case UnityConstants.Type.Float:
					if (float.TryParse(number, out float floatValue))
					{
						property.floatValue = isPercentage ? floatValue / 100f : floatValue;
						return true;
					}
					else
					{
						return false;
					}

				case UnityConstants.Type.Double:
					if (double.TryParse(number, out double doubleValue))
					{
						property.doubleValue = isPercentage ? doubleValue / 100d : doubleValue;
						return true;
					}
					else
					{
						return false;
					}

				default:
					if (float.TryParse(number, out float defaultValue))
					{
						property.floatValue = isPercentage ? defaultValue / 100f : defaultValue;
						return true;
					}
					else
					{
						return false;
					}
			}
		}

		// Detects a trailing % on a pasted/imported value. Float setters divide the result by 100 (a "3%" cell is
		// 0.03 in a spreadsheet); integer setters can't hold a fraction so they just use the stripped number.
		// Manual field entry instead goes through Unity's number field, which strips the % itself and never reaches this path.
		private static bool TryStripPercentage(string value, out string number)
		{
			if (!string.IsNullOrEmpty(value))
			{
				var trimmed = value.TrimEnd();
				if (trimmed.EndsWith("%", StringComparison.Ordinal))
				{
					number = trimmed.Substring(0, trimmed.Length - 1);
					return true;
				}
			}
			number = value;
			return false;
		}

		public static bool TrySetIntValue(this SerializedProperty property, string value)
		{
			// Integer types can't hold a fraction, so a trailing % is simply stripped (a 3% cell pastes as 3).
			TryStripPercentage(value, out var number);
			switch (property.type)
			{
				case UnityConstants.Type.Int:
					if (int.TryParse(number, out int intValue))
					{
						property.intValue = intValue;
						return true;
					}
					else
					{
						return false;
					}

				case UnityConstants.Type.Long:
					if (long.TryParse(number, out long longValue))
					{
						property.longValue = longValue;
						return true;
					}
					else
					{
						return false;
					}

#if UNITY_2022_1_OR_NEWER
				case UnityConstants.Type.UInt:
					if (uint.TryParse(number, out uint uintValue))
					{
						property.uintValue = uintValue;
						return true;
					}
					else
					{
						return false;
					}

				case UnityConstants.Type.ULong:
					if (ulong.TryParse(number, out ulong ulongValue))
					{
						property.ulongValue = ulongValue;
						return true;
					}
					else
					{
						return false;
					}
#endif
				default:
					if (int.TryParse(number, out int defaultValue))
					{
						property.intValue = defaultValue;
						return true;
					}
					else
					{
						return false;
					}
			}
		}

		public static Gradient GetGradientValue(this SerializedProperty property)
		{
#if UNITY_2022_1_OR_NEWER
			return property.gradientValue;
#else
			var propertyInfo = property.GetGradientPropertyInfo();
			return propertyInfo?.GetValue(property, null) as Gradient;
#endif
		}

		public static void SetGradientValue(this SerializedProperty property, Gradient newValue)
		{
#if UNITY_2022_1_OR_NEWER
			property.gradientValue = newValue;
#else
			var propertyInfo = property.GetGradientPropertyInfo();
			propertyInfo?.SetValue(property, newValue);
#endif
		}

#if !UNITY_2022_1_OR_NEWER
		private static System.Reflection.PropertyInfo GetGradientPropertyInfo(this SerializedProperty property)
		{
			var propertyName = "gradientValue";
			var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
			System.Reflection.PropertyInfo propertyInfo = typeof(SerializedProperty).GetProperty(propertyName, bindingFlags);
			if (propertyInfo != null)
			{
				return propertyInfo;
			}
			else
			{
				Debug.LogWarning($"Unable to find property '{propertyName}' for {nameof(SerializedProperty)} at {property.propertyPath}.");
				return null;
			}
		}
#endif

		public static bool IsArraySizeOrElement(this SerializedProperty property)
		{
			return property.propertyType == SerializedPropertyType.ArraySize || property.IsArrayElement();
		}

		public static bool IsArrayElement(this SerializedProperty property)
		{
			return IsArrayElement(property.propertyPath);
		}

		public static bool IsArrayElement(string propertyPath)
		{
			return propertyPath.Contains('[');
		}

		public static bool IsAssetReference(this SerializedProperty property)
		{
			return property.type.Contains(UnityConstants.Type.AssetReference) && property.FindPropertyRelative(UnityConstants.Field.AssetRefGuid) != null;
		}

		public static bool IsInputFieldProperty(this SerializedProperty property, bool isScriptableObject)
		{
			var propertyType = property.GetSheetsPropertyType(isScriptableObject);
			return s_InputFieldPropertyTypes.Contains(propertyType);
		}

		public static bool IsPropertyVisible(this SerializedProperty property, bool showArrays, bool showReadOnly, out bool isReadOnly)
		{
			var isArraySizeOrElement = property.IsArraySizeOrElement();
			isReadOnly = property.IsReadOnly();
			return (showArrays || !isArraySizeOrElement) && (showReadOnly || !isReadOnly) && !property.hasVisibleChildren
				&& property.propertyType != SerializedPropertyType.Generic && property.propertyType != SerializedPropertyType.ManagedReference;
		}

		public static bool IsReadOnly(this SerializedProperty property)
		{
			return s_ReadOnlyUnityFields.Contains(property.name) || !property.editable;
		}

		public static bool IsTypeDouble(this SerializedProperty property)
		{
			return property.type == UnityConstants.Type.Double;
		}

		public static bool IsTypeFloat(this SerializedProperty property)
		{
			return property.type == UnityConstants.Type.Float;
		}

		public static bool IsTypeInt(this SerializedProperty property)
		{
			return property.type == UnityConstants.Type.Int;
		}

		public static bool IsTypeLong(this SerializedProperty property)
		{
			return property.type == UnityConstants.Type.Long;
		}

		public static bool IsTypeUInt(this SerializedProperty property)
		{
			return property.type == UnityConstants.Type.UInt;
		}

		public static bool IsTypeULong(this SerializedProperty property)
		{
			return property.type == UnityConstants.Type.ULong;
		}

		public static bool TryGetEnumType(this SerializedProperty property, Object rootObject, out Type enumType)
		{
			var enumPropertyPath = property.propertyPath;
			enumType = ReflectionUtility.GetNestedFieldType(rootObject.GetType(), enumPropertyPath);
			if (enumType != null && enumType.IsEnum)
			{
				return true;
			}
			else
			{
				Debug.LogWarning($"Property on {nameof(Object)} {rootObject.name} at path {enumPropertyPath} is not a valid type of enum.");
				return false;
			}
		}

		// Various default Unity Engine Components have different limitations on how backing fields are serialized and accessed.
		private static bool IsDefaultUnityEngineType(Object rootObject)
		{
			return IsDefaultUnityEngineType(rootObject, out string fullTypeName);
		}

		private static bool IsDefaultUnityEngineType(Object rootObject, out string fullTypeName)
		{
			fullTypeName = rootObject.GetType().FullName;
			return fullTypeName.StartsWith(UnityConstants.Type.UnityEngine);
		}

		// Handle special case for TMPro alignment enum properties.
		private static bool IsAlignmentProperty(string propertyPath)
		{
			return propertyPath == UnityConstants.Field.HorizontalAlignment || propertyPath == UnityConstants.Field.VerticalAlignment || propertyPath == UnityConstants.Field.TextAlignment;
		}

		private static Type GetSharedTableCollectionType()
		{
			if (s_StringTableCollectionType == null)
			{
				s_StringTableCollectionType = Type.GetType($"{UnityConstants.Type.UnityLocalizationSharedTableData}, {UnityConstants.Type.UnityLocalization}");
			}
			return s_StringTableCollectionType;
		}

		// Get GUID from "GUID:xxxx" or return null.
		private static string GetGuidFrom(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return null;
			}
			if (!value.StartsWith(UnityConstants.Guid, StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}
			return value.Substring(UnityConstants.Guid.Length);
		}

		private static void DrawLocalizationTablePicker(Rect rect, SerializedProperty property, AssetPreviewSettings previewSettings)
		{
			var sharedTableType = GetSharedTableCollectionType();

			// If the localization package isn't installed then fallback to a simple text field.
			if (sharedTableType == null)
			{
				property.stringValue = EditorGUI.TextField(rect, property.stringValue);
				return;
			}

			Object currentValue = null;
			var guid = GetGuidFrom(property.stringValue);
			if (!string.IsNullOrEmpty(guid))
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				currentValue = AssetDatabase.LoadAssetAtPath(path, sharedTableType);
			}

			if (rect.height > EditorGUIUtility.singleLineHeight)
			{
				DrawUtility.TableAssetPreview(currentValue, rect, previewSettings);
				rect.height = EditorGUIUtility.singleLineHeight;
			}

			var newValue = EditorGUI.ObjectField(rect, currentValue, sharedTableType, false);
			if (currentValue != newValue)
			{
				if (newValue == null)
				{
					property.stringValue = string.Empty;
				}
				else
				{
					var path = AssetDatabase.GetAssetPath(newValue);
					var newGuid = AssetDatabase.AssetPathToGUID(path);
					property.stringValue = $"{UnityConstants.Guid}{newGuid}";
				}
			}
		}

		private static void DrawTableEntryKeyDropdown(Rect rect, SerializedProperty property)
		{
			var sharedTableType = GetSharedTableCollectionType();

			// If the localization package isn't installed then fallback to a simple long field.
			if (sharedTableType == null)
			{
				property.longValue = EditorGUI.LongField(rect, property.longValue);
				return;
			}

			var tableRefPath = property.propertyPath.Replace(UnityConstants.Field.TableEntryReferenceKeyId, UnityConstants.Field.TableReferenceCollectionName);
			var tableRefProp = property.serializedObject.FindProperty(tableRefPath);
			var guid = GetGuidFrom(tableRefProp.stringValue);

			if (string.IsNullOrEmpty(guid))
			{
				EditorGUI.LabelField(rect, "<No Table Selected>");
				return;
			}

			var path = AssetDatabase.GUIDToAssetPath(guid);
			var sharedTable = AssetDatabase.LoadAssetAtPath(path, sharedTableType);
			if (sharedTable == null)
			{
				EditorGUI.LabelField(rect, "<Missing Table>");
				return;
			}

			var sharedEntriesProp = sharedTableType.GetProperty(UnityConstants.Field.SharedTableDataEntries);
			if (sharedEntriesProp == null)
			{
				EditorGUI.LabelField(rect, "<Entries Not Found>");
				return;
			}

			var entries = sharedEntriesProp.GetValue(sharedTable) as IEnumerable<object>;
			if (entries == null)
			{
				EditorGUI.LabelField(rect, "<No Entries>");
				return;
			}

			var ids = new List<long>();
			var labels = new List<string>();

			var entryType = entries.GetType().GetGenericArguments()[0];
			var idProp = entryType.GetProperty(UnityConstants.Field.SharedTableDataId);
			var keyProp = entryType.GetProperty(UnityConstants.Field.SharedTableDataKey);

			foreach (var entry in entries)
			{
				var id = (long) idProp.GetValue(entry);
				ids.Add(id);

				var key = (string) keyProp.GetValue(entry);
				labels.Add(key);
			}

			if (ids.Count <= 0)
			{
				EditorGUI.LabelField(rect, "<Empty Table>");
				return;
			}

			var currentIndex = ids.IndexOf(property.longValue);
			var newIndex = EditorGUI.Popup(rect, currentIndex, labels.ToArray());
			if (currentIndex != newIndex && newIndex >= 0 && newIndex < ids.Count)
			{
				property.longValue = ids[newIndex];
			}
		}
	}
}
