using System.Collections.Generic;
using UMA.Editors;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UMA.CharacterSystem.Editors 
{

	public class UMAAssetIndexerEditor : EditorWindow 
	{
		Dictionary<System.Type, bool> Toggles = new Dictionary<System.Type, bool>();
		UMAAssetIndexer _UAI;
		List<Object> AddedDuringGui = new List<Object>();  
		List<System.Type> AddedTypes = new List<System.Type>();
		List<AssetItem> DeletedDuringGUI = new List<AssetItem>();
		List<System.Type> RemovedTypes = new List<System.Type>();
		Dictionary<System.Type, List<bool>> TypeCheckboxes = new Dictionary<System.Type, List<bool>>();
		public Vector2 scrollPosition = Vector2.zero;
		delegate void ProcessAssetItem(System.Type type, int i, AssetItem a);

		private enum FilterType { all=0, addressable, notaddressable, always, notalways, hasreference, noreference };
		private string[] FilterLookup = { "All", "Addressable","Not Addressable", "Always Loaded", "Not Always Loaded", "With References", "Without References" };
		// dictionary of type
		// contains list of bools

		private FilterType MetaFilter = FilterType.all;
		public string Filter = "";
		public bool IncludeText;

		int MetaFilterIndex;
		int NotInBuildCount = 0;
		int AlwaysLoadedCount = 0;
		int AddressableCount = 0;
		int ItemCount = 0;

		UMAAssetIndexer UAI 
		{
			get
			{
				if (_UAI == null)
				{
					_UAI = UMAAssetIndexer.Instance;
				}	
				return _UAI;
			}
		}

		public UMAMaterial SelectedMaterial = null;
		void OnGUI()
		{
			try
			{
				if (UAI == null)
				{
					return;
				}
			}
			catch
			{
				return;
			}
			GUILayout.Space(16);
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.Label("UMA Global Library");
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.Space(16);

			EditorGUILayout.HelpBox("Note: Build References MUST be added when building your application. On large applications this can be slow, so it is recommend to only have build references when actually building.", MessageType.Warning);

			GUILayout.Space(16);

			scrollPosition = GUILayout.BeginScrollView(scrollPosition,GUIStyle.none);
			DoGUI();
			GUILayout.EndScrollView();
		}


		#region Drag Drop
		private void DropAreaGUI(Rect dropArea)
		{

			var evt = Event.current;

			if (evt.type == EventType.DragUpdated)
			{
				if (dropArea.Contains(evt.mousePosition))
				{
					DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
				}
			}

			if (evt.type == EventType.DragPerform)
			{
				if (dropArea.Contains(evt.mousePosition))
				{
					DragAndDrop.AcceptDrag();
					AddedDuringGui.Clear();
					UnityEngine.Object[] draggedObjects = DragAndDrop.objectReferences as UnityEngine.Object[];
					for (int i = 0; i < draggedObjects.Length; i++)
					{
						if (draggedObjects[i])
						{
							AddedDuringGui.Add(draggedObjects[i]);

							var path = AssetDatabase.GetAssetPath(draggedObjects[i]);
							if (System.IO.Directory.Exists(path))
							{
								RecursiveScanFoldersForAssets(path);
							}
						}
					}
				}
			}
		}

		private void DropAreaType(Rect dropArea)
		{

			var evt = Event.current;

			if (evt.type == EventType.DragUpdated)
			{
				if (dropArea.Contains(evt.mousePosition))
				{
					DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
				}
			}

			if (evt.type == EventType.DragPerform)
			{
				if (dropArea.Contains(evt.mousePosition))
				{
					DragAndDrop.AcceptDrag();
					AddedTypes.Clear();
					UnityEngine.Object[] draggedObjects = DragAndDrop.objectReferences as UnityEngine.Object[];
					for (int i = 0; i < draggedObjects.Length; i++)
					{
						if (draggedObjects[i])
						{
							System.Type sType = draggedObjects[i].GetType();

							AddedTypes.Add(sType);
						}
					}
				}
			}
		}

		private void AddObject(Object draggedObject)
		{
			System.Type type = draggedObject.GetType();
			if (UAI.IsIndexedType(type))
			{
				UAI.EvilAddAsset(type, draggedObject);
			}
		}

		private void RecursiveScanFoldersForAssets(string path)
		{
			var assetFiles = System.IO.Directory.GetFiles(path);

			foreach (var assetFile in assetFiles)
			{
				string Extension = System.IO.Path.GetExtension(assetFile).ToLower();
				if (Extension == ".asset" || Extension == ".controller" || Extension == ".txt")
				{
					Object o = AssetDatabase.LoadMainAssetAtPath(assetFile);

					if (o)
					{
						AddedDuringGui.Add(o);
					}
				}
			}
			foreach (var subFolder in System.IO.Directory.GetDirectories(path))
			{
				RecursiveScanFoldersForAssets(subFolder.Replace('\\', '/'));
			}
		}
		#endregion

		private void Cleanup()
		{
			if (AddedDuringGui.Count > 0 || DeletedDuringGUI.Count > 0 || AddedTypes.Count > 0 || RemovedTypes.Count > 0)
			{
				for (int i = 0; i < AddedDuringGui.Count; i++)
				{
					EditorUtility.DisplayProgressBar("Adding Items to Global Library.", AddedDuringGui[i].name, ((float)i / (float)AddedDuringGui.Count));
					AddObject(AddedDuringGui[i]);
				}
				EditorUtility.ClearProgressBar();

				foreach (AssetItem ai in DeletedDuringGUI)
				{
					UAI.RemoveAsset(ai._Type, ai._Name);
				}

				foreach (System.Type st in RemovedTypes)
				{
					UAI.RemoveType(st);
				}

				foreach (System.Type st in AddedTypes)
				{
					UAI.AddType(st);
				}

				AddedTypes.Clear();
				RemovedTypes.Clear();
				DeletedDuringGUI.Clear();
				AddedDuringGui.Clear();

				UAI.ForceSave();
				Repaint();
			}
		}

		private void SetFoldouts(bool Value)
		{
			System.Type[] Types = UAI.GetTypes();
			foreach (System.Type t in Types)
			{
				Toggles[t] = Value;
			}
		}

		public void DoGUI()
		{
			if (Event.current.type == EventType.Layout)
			{
				Cleanup();
			}

			ShowTypes();

			// Draw and handle the Drag/Drop
			GUILayout.Space(20);
			Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
			GUI.Box(dropArea, "Drag Indexable Assets here. Non indexed assets will be ignored.");
			GUILayout.Space(20);
			DropAreaGUI(dropArea);

			System.Type[] Types = UAI.GetTypes();
			if (Toggles.Count != Types.Length) SetFoldouts(false);

			GUILayout.BeginHorizontal();
			//if (GUILayout.Button("Reindex Names"))
			//{
			//	UAI.RebuildIndex();
			//}

			GUILayout.EndHorizontal();


			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Rebuild From Project (Adds Everything)"))
			{
				UAI.Clear();
				UAI.AddEverything(IncludeText);
				Resources.UnloadUnusedAssets();
			}
			IncludeText = GUILayout.Toggle(IncludeText, "Include TextAssets");
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Repair and Remove invalid items"))
			{
				UAI.RepairAndCleanup();
				Resources.UnloadUnusedAssets();
			}
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Clean/Regenerate Addressables"))
			{
				UAI.CleanupAddressables();
				UAI.GenerateAddressables();
				Resources.UnloadUnusedAssets();
			}
            if (GUILayout.Button("Delete Empty"))
            {
				UAI.CleanupAddressables(true);
            }
            GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Add Build References"))
			{
				UAI.AddReferences();
				Resources.UnloadUnusedAssets();
			}
			if (GUILayout.Button("Force Add Refs"))
			{
				UAI.AddReferences(true);
				Resources.UnloadUnusedAssets();
			}
			if (GUILayout.Button("Clear References"))
			{
				UAI.ClearReferences();
				Resources.UnloadUnusedAssets();
			}
			if (GUILayout.Button("Empty Index"))
			{
				UAI.Clear();
			}
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Collapse All"))
			{
				SetFoldouts(false);
			}

			if (GUILayout.Button("Expand All"))
			{
				SetFoldouts(true);
			}

			GUILayout.EndHorizontal();

			//bool PreSerialize = UAI.SerializeAllObjects;
			////UAI.SerializeAllObjects = EditorGUILayout.Toggle("Serialize for build (SLOW)", UAI.SerializeAllObjects);
			//if (UAI.SerializeAllObjects != PreSerialize)
			//{
			//	UAI.ForceSave();
			//}
			UAI.AutoUpdate = EditorGUILayout.Toggle("Process Updates", UAI.AutoUpdate);
			GUILayout.BeginHorizontal();

			Filter = EditorGUILayout.TextField("Filter Library", Filter);

			MetaFilterIndex = EditorGUILayout.Popup(MetaFilterIndex, FilterLookup,GUILayout.Width(100));
			MetaFilter = (FilterType)MetaFilterIndex;

			GUI.SetNextControlName("TheDumbUnityBuggyField");
			if (GUILayout.Button("-", GUILayout.Width(20)))
			{
				Filter = "";
				// 10 year old unity bug that no one wants to fix.
				GUI.FocusControl("TheDumbUnityBuggyField");
			}
			GUILayout.EndHorizontal();

			bool HasErrors = false;
			string ErrorTypes = "";
			NotInBuildCount = 0;
			AlwaysLoadedCount = 0;
			AddressableCount = 0;
			ItemCount = 0;

			foreach (System.Type t in Types)
			{
				if (t != typeof(AnimatorController) && t!= typeof(AnimatorOverrideController)) // Somewhere, a kitten died because I typed that.
				{
					if (ShowArray(t, Filter))
					{
						HasErrors = true;
						ErrorTypes += t.Name +" ";
					}
				}
			}

			string bldMessage = string.Format("Items:{0} Always:{1} NotIncluded:{2} Addressable:{3}", ItemCount, AlwaysLoadedCount, NotInBuildCount, AddressableCount);
			GUILayout.BeginHorizontal();
			if (HasErrors)
			{
				GUILayout.Label(ErrorTypes + " Have error items! "+bldMessage);
			}
			else
			{
				GUILayout.Label("No errors. " +bldMessage);
			}
			GUILayout.EndHorizontal();
		}

		public bool IsFiltered(AssetItem ai)
		{
			if (MetaFilter == FilterType.addressable)
			{
				if (!ai.IsAddressable)
					return true;
			}

			if (MetaFilter == FilterType.always)
			{
				if (!ai.IsAlwaysLoaded)
					return true;
			}

			if (MetaFilter == FilterType.notaddressable)
			{
				if (ai.IsAddressable)
					return true;
			}

			if (MetaFilter == FilterType.notalways)
			{
				if (ai.IsAlwaysLoaded)
					return true;
			}

			if (MetaFilter == FilterType.hasreference)
			{
				if (ai._SerializedItem == null)
					return true;
			} 

			if (MetaFilter == FilterType.noreference)
			{
				if (ai._SerializedItem != null)
					return true;
			}
			return false;
		}

		public bool ShowArray(System.Type CurrentType, string Filter)
        {
            bool HasFilter = false;
            bool NotFound = false;
            string actFilter = Filter.Trim().ToLower();
            if (actFilter.Length > 0)
                HasFilter = true;

            Dictionary<string, AssetItem> TypeDic = UAI.GetAssetDictionary(CurrentType);

            if (!TypeCheckboxes.ContainsKey(CurrentType))
            {
                TypeCheckboxes.Add(CurrentType, new List<bool>());
            }

            List<AssetItem> Items = new List<AssetItem>();
            Items.AddRange(TypeDic.Values);

            int NotInBuild = 0;
			int Addressable = 0;
			int AlwaysIncluded = 0;
			int LocalItems = 0;
            int VisibleItems = 0;
            foreach (AssetItem ai in Items)
            {
				LocalItems++;
				Object test = ai._SerializedItem;

				System.Object o = (System.Object)(test);

				if (IsFiltered(ai))
				{
					continue;
				}

				if (ai.IsAddressable) Addressable++;
				if (ai.IsAlwaysLoaded) AlwaysIncluded++;
				if (o == null && ai.IsAddressable == false)
                {
                    NotInBuild++;
                }
                string Displayed = ai.ToString(UMAAssetIndexer.SortOrder);
                if (HasFilter && (!Displayed.ToLower().Contains(actFilter)))
                {
                    continue;
                }
                VisibleItems++;
            }

            if (TypeCheckboxes[CurrentType].Count != VisibleItems)
            {
                TypeCheckboxes[CurrentType].Clear();
                TypeCheckboxes[CurrentType].AddRange(new bool[VisibleItems]);
            }

			ItemCount += LocalItems;
            NotInBuildCount += NotInBuild;
			AlwaysLoadedCount += AlwaysIncluded;
			AddressableCount += Addressable;

            GUILayout.BeginHorizontal(EditorStyles.toolbarButton);
            GUILayout.Space(10);
            Toggles[CurrentType] = EditorGUILayout.Foldout(Toggles[CurrentType], CurrentType.Name + ":  " + VisibleItems + "/" + TypeDic.Count + " Item(s). " + NotInBuild + " missing. "+Addressable+" Addressable. "+AlwaysIncluded+" Always In Resources");
            GUILayout.EndHorizontal();



            if (Toggles[CurrentType])
            {
                Items.Sort();
                GUIHelper.BeginVerticalPadded(5, new Color(0.75f, 0.875f, 1f));
                GUILayout.BeginHorizontal();
                GUILayout.Label("Sorted By: " + UMAAssetIndexer.SortOrder, GUILayout.MaxWidth(160));
                foreach (string s in UMAAssetIndexer.SortOrders)
                {
                    if (GUILayout.Button(s, GUILayout.Width(80)))
                    {
                        UMAAssetIndexer.SortOrder = s;
                    }
                }
                GUILayout.EndHorizontal();

                int CurrentVisibleItem = 0;
                foreach (AssetItem ai in Items)
                {
                    string lblBuild = "B-";
                    string lblVal = ai.ToString(UMAAssetIndexer.SortOrder);
                    if (HasFilter && (!lblVal.ToLower().Contains(actFilter)))
                        continue;

					if (IsFiltered(ai))
					{
						continue;
					}

					if (ai._Name == "< Not Found!>")
                    {
                        NotFound = true;
                    }
                    GUILayout.BeginHorizontal(EditorStyles.textField);


                    TypeCheckboxes[CurrentType][CurrentVisibleItem] = EditorGUILayout.Toggle(TypeCheckboxes[CurrentType][CurrentVisibleItem++], GUILayout.Width(20));
					if (ai.IsAddressable)
					{
						lblVal += "<Addressable>";
						lblBuild = "NF";
					}
					else if (ai._SerializedItem == null)
                    {
                        lblVal += "<Not in Build>";
                        lblBuild = "B+";
                    }


					if (GUILayout.Button(lblVal, EditorStyles.label))
                    {
                        Object o = AssetDatabase.LoadMainAssetAtPath(ai._Path);
                        EditorGUIUtility.PingObject(o);
                        Selection.activeObject = o;
                    }

					GUILayout.Label("Always ", GUILayout.Width(60.0f));
					ai.IsAlwaysLoaded = EditorGUILayout.Toggle(ai.IsAlwaysLoaded, GUILayout.Width(20));

					if (GUILayout.Button("I", GUILayout.Width(20.0f)))
                    {
                        Object o = AssetDatabase.LoadMainAssetAtPath(ai._Path);
                        InspectorUtlity.InspectTarget(o);
                    }
                    if (GUILayout.Button(lblBuild, GUILayout.Width(35)))
                    {
                        if (ai._SerializedItem == null)
                        {
                            if (!ai.IsAssetBundle)
                                ai.CacheSerializedItem();
                        }
                        else
                        {
                            ai.ReleaseItem();
                        }

                    }
                    if (GUILayout.Button("-", GUILayout.Width(20.0f)))
                    {
                        DeletedDuringGUI.Add(ai);
                    }
                    GUILayout.EndHorizontal();
                }

                GUILayout.BeginHorizontal();
                if (NotFound)
                {
                    GUILayout.Label("Warning - Some items not found!");
                }
                else
                {
                    GUILayout.Label("All Items appear OK");
                }
                GUILayout.EndHorizontal();

                GUIHelper.BeginVerticalPadded(5, new Color(0.65f, 0.65f, 0.65f));
                GUILayout.Label("Utilities");
                GUILayout.Space(10);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All"))
                {
                    ProcessItems(CurrentType, null, HasFilter, actFilter, Items, SelectItems);
                }
                if (GUILayout.Button("Select None"))
                {
                    ProcessItems(CurrentType, null, HasFilter, actFilter, Items, DeselectItems);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();

				if (GUILayout.Button("Remove Checked"))
                {
                    ProcessItems(CurrentType, TypeCheckboxes[CurrentType], HasFilter, actFilter, Items, RemoveChecked);
                }

                if (GUILayout.Button("Force Save"))
                {
                    ProcessItems(CurrentType, TypeCheckboxes[CurrentType], HasFilter, actFilter, Items, ForceSerialize);
                    AssetDatabase.SaveAssets();
                }
                EditorGUILayout.EndHorizontal();


                if (CurrentType == typeof(SlotDataAsset) || CurrentType == typeof(OverlayDataAsset))
                {
                    EditorGUILayout.BeginHorizontal();
                    SelectedMaterial = (UMAMaterial)EditorGUILayout.ObjectField(SelectedMaterial, typeof(UMAMaterial), false);
                    GUILayout.Label("Apply to checked: ");
                    if (GUILayout.Button("Unassigned"))
                    {
                        ProcessItems(CurrentType, TypeCheckboxes[CurrentType], HasFilter, actFilter, Items, SetItemNullMaterial);
                    }
                    if (GUILayout.Button("All"))
                    {
                        ProcessItems(CurrentType, TypeCheckboxes[CurrentType], HasFilter, actFilter, Items, SetItemMaterial);
                    }
                    EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					if (GUILayout.Button("Remove Orphaned Items"))
					{
						// Remove orphans here. 
						// lots/Overlays that are not addressable, and not always.
						UAI.CleanupOrphans(CurrentType);
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.HelpBox("Removing Orphaned Items should be done AFTER you have marked the Races and Wardrobe items you want to include as 'Always Included', and after you have built Addressable Bundles!", MessageType.Info);
				}
				GUIHelper.EndVerticalPadded(5);


                GUIHelper.EndVerticalPadded(5);
            }
            return NotFound;
        }

        private void ForceSerialize(System.Type type, int i, AssetItem a)
        {
            EditorUtility.SetDirty(a.Item);
        }

        private void RemoveChecked(System.Type type, int i, AssetItem ai)
		{
			DeletedDuringGUI.Add(ai);
		}

		private void SelectItems(System.Type CurrentType, int i, AssetItem a)
		{
			TypeCheckboxes[CurrentType][i] = true;
		}

		private void DeselectItems(System.Type CurrentType, int i, AssetItem a)
		{
			TypeCheckboxes[CurrentType][i] = false;
		}

		void SetItemNullMaterial(System.Type type, int i, AssetItem ai)
		{
			if (ai._Type == typeof(SlotDataAsset))
			{
				if ((ai.Item as SlotDataAsset).material == null)
					(ai.Item as SlotDataAsset).material = SelectedMaterial;
			}
			if (ai._Type == typeof(OverlayDataAsset))
			{
				if ((ai.Item as OverlayDataAsset).material == null)
					(ai.Item as OverlayDataAsset).material = SelectedMaterial;
			}
		}
		void SetItemMaterial(System.Type type, int i, AssetItem ai)
		{
			if (ai._Type == typeof(SlotDataAsset))
			{
				(ai.Item as SlotDataAsset).material = SelectedMaterial;
			}
			if (ai._Type == typeof(OverlayDataAsset))
			{
				(ai.Item as OverlayDataAsset).material = SelectedMaterial;
			}
		}

		private void ProcessItems(System.Type type, List<bool> Selected, bool HasFilter, string actFilter, List<AssetItem> Items, ProcessAssetItem p)
		{
			int i = 0;
			foreach (AssetItem ai in Items)
			{
				string lblVal = ai.ToString(UMAAssetIndexer.SortOrder);
				if (HasFilter && (!lblVal.ToLower().Contains(actFilter)))
					continue;
				if (Selected != null)
				{
					if (Selected[i])
					{
						p(type, i, ai);
					}
				}
				else
				{
					p(type, i, ai);
				}
				++i;
			}
		}

		public bool bShowTypes;

		public void ShowTypes()
		{
			GUILayout.BeginHorizontal(EditorStyles.toolbarButton);
			GUILayout.Space(10);
			bShowTypes = EditorGUILayout.Foldout(bShowTypes, "Additional Indexed Types");
			GUILayout.EndHorizontal();

			if (bShowTypes)
			{
				GUIHelper.BeginVerticalPadded(10, new Color(0.75f, 0.875f, 1f));

				// Draw and handle the Drag/Drop
				GUILayout.Space(20);
				Rect dropTypeArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
				GUI.Box(dropTypeArea, "Drag a single type here to start indexing that type.");
				GUILayout.Space(20);
				DropAreaType(dropTypeArea);
				foreach (string s in UAI.IndexedTypeNames)
				{
					System.Type CurrentType = System.Type.GetType(s);
					GUILayout.BeginHorizontal(EditorStyles.textField);
					GUILayout.Label(CurrentType.ToString(), GUILayout.MinWidth(240));
					if (GUILayout.Button("-", GUILayout.Width(20.0f)))
					{
						RemovedTypes.Add(CurrentType);
					}
					GUILayout.EndHorizontal();
				}
				GUIHelper.EndVerticalPadded(10);
			}
		}
		[MenuItem ("UMA/Global Library Window", priority = 10)]
		public static void  ShowWindow () 
		{
			UMAAssetIndexerEditor window = EditorWindow.GetWindow<UMAAssetIndexerEditor>();
			Texture icon = AssetDatabase.LoadAssetAtPath<Texture> ("Assets/UMA/InternalDataStore/UMA32.png");
			// Create the instance of GUIContent to assign to the window. Gives the title "RBSettings" and the icon
			GUIContent titleContent = new GUIContent ("UMA Library", icon);
			window.titleContent = titleContent;
		}
	}
}