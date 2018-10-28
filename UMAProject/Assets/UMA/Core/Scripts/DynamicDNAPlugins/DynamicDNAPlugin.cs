﻿using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UMA
{

	[System.Serializable]
	public abstract class DynamicDNAPlugin : ScriptableObject
	{
		//=====================================================================//
		//A DynamicDNAPlugin is always a LIST of a type of 'DNA Converter' (an abstract concept- it can be any type)
		//A 'DNA Converter' converts dna values into modifications to the character
		//For example in UMA currently we have a SkeletonModifier that converts dna values into modifications to the skeletons bones
		//Or a DNAMorphset converts dna values into weights for UMABonePoses and Blendshapes
		//So those are examples of a 'DNA Converter' and a DynamicDNAPlugin is most basically a list of one of those kinds of things, 
		//along with an ApplyDNA method to apply them
		//The plugin concept places no restrictions on what those 'DNA Converters' might be or do.
		//But it does expect that a DynamicDNAPlugin contain a list of them and requires one method and one property in order to integrate them into the system.

		//DynamicDNAPlugins only have to derive from DynamicDNAPlugin and they do not need an associated inspector. 
		//Any new plugins are automatically found and are available in to add to the DynamicDNAConverterAsset via its inspector

		//DynamicDNAPlugins also have a MasterWeight. Setting this to zero disables the plugin, but the master weight can itself be hooked up to a dna value.
		//By doing this different characters can control how much a set of Skeleton Modifiers or MorphSets should apply to them in their current state.
		//For example a charcaters 'Claws' dna might do nothing while its human, but alot when it is a 'werewolf' (i.e. its 'werewolf' dna is turned up)

		//The system also introduces DNAEvaluator, which is a super flexible field that performs math calculations on a dna value using a customizable animation curve.
		//This is so that there is no need to have any extra behaviours or code in order to interpret dna values in a certain way.
		//So the user is not overwhelmed by the complexity of using animation curves for math, DNAEvaluator uses DNAEvaluationGraphs which are preset curves
		//with nice friendly names and tool tips. As coders we can set DNAEvaluationGraph fields to use one of our predefined defaults.

		//DynamicDNAPlugins are assigned to a DynamicDNAConverterAsset which calls ApplyDNA on each plugin in turn at runtime, 
		//and which at edit time looks after the creation of the Plugin Assets and its own Plugins list
		//A DynamicDNAConverterAsset is assigned to a DynamicDNAConverterBehaviour which triggers the ApplyDNA action on the DynamicDNAConverterAsset

		#region ABSTRACT PROPERTIES AND METHODS

		//It is REQUIRED that all DynamicDNAPlugins have these two Propeties and Methods (and thats it!)

		/// <summary>
		/// Returns a dictionary of all the dna names in use by the plugin and the indexes of the entries in its converter list that reference them
		/// </summary>
		public abstract Dictionary<string, List<int>> IndexesForDnaNames { get; }

		/// <summary>
		/// Called by the converter this plugin is assigned to. Applys the plugins list of converters to the character
		/// </summary>
		public abstract void ApplyDNA(UMAData umaData, UMASkeleton skeleton, int dnaTypeHash);

		#endregion

		#region PUBLIC MEMBERS

		/// <summary>
		/// All DynamicDNAPlugins have a 'MasterWeight' this makes it possible to disable them completely, or only enable them when certain dna conditions are met
		/// </summary>
		[Tooltip("The master weight controls how much all the converters in this group are applied. You can disable a set of converters by making the master weight zero. Or you can hook the master weight up to a characters dna so the converters only apply when that dna has a certain value.")]
		[SerializeField]
		public MasterWeight masterWeight = new MasterWeight();

		#endregion

		#region PRIVATE MEMBERS

		[SerializeField]
		private DynamicDNAConverterController _converterAsset;

		#endregion

		#region PUPLIC PROPERTIES

		/// <summary>
		/// The converter asset this plugin has been assigned to. This property is set by the converter when it is inspected or starts
		/// </summary>
		public DynamicDNAConverterController converterAsset
		{
			get { return _converterAsset; }
			set { _converterAsset = value; }
		}

		public DynamicUMADnaAsset DNAAsset
		{
			get
			{
				if (_converterAsset != null)
					return _converterAsset.dnaAsset;
				return null;
			}
		}

		/*public UMAData umaData
		{
			get
			{
				if (_converterAsset != null)
					return _converterAsset.umaData;
				return null;
			}
		}*/

		#endregion

		#region VIRTUAL PROPERTIES

		//Its is OPTIONAL for any DynamicDNAPlugin to override these methods

#if UNITY_EDITOR

		public virtual string PluginHelp { get { return ""; } }

		public virtual float GetListHeaderHeight
		{
			get
			{
				return 0f;
			}
		}

		public virtual float GetListFooterHeight
		{
			get
			{
				return 13f;
			}
		}

		/// <summary>
		/// Gets the height of the 'Help' info that will be drawn by DrawPluginHelp.
		/// </summary>
		public virtual float GetPluginHelpHeight
		{
			get
			{
				//by default this will return the height of a help box that contains the PluginHelp string
				return EditorStyles.helpBox.CalcHeight(new GUIContent(PluginHelp, EditorGUIUtility.FindTexture("console.infoicon")), Screen.width - EditorGUIUtility.FindTexture("console.infoicon").width) + (EditorGUIUtility.singleLineHeight / 2);
			}
		}

		//This is a string array because different plugins might want to make different import methods.
		//the plugins ImportSettings method will just be sent the index of the choice, then its up to the method what to do
		/// <summary>
		/// Standard ImportSettingsMethods are [0]Add [1]Replace
		/// Override this if your plugins ImportSettings method uses different options
		/// </summary>
		public virtual string[] ImportSettingsMethods
		{
			get
			{
				return new string[]
				{
				"Add",
				"Replace"
				};
			}
		}

#endif

		#endregion

		#region VIRTUAL METHODS

#if UNITY_EDITOR
		/// <summary>
		/// Override this method if DynamicDNAPluginInspector is not finding your list of converters automatically
		/// </summary>
		public virtual SerializedProperty GetConvertersListProperty(SerializedObject pluginSO)
		{
			//if overidden you should do something like 
			//return pluginSO.FindPropertyRelative("nameOfMyConverterList");

			//By default gets the first kind of valid array in the plugin.
			//Since plugins should always be a list of converters first and foremost this should usually work
			SerializedProperty it = pluginSO.GetIterator();
			it.Next(true);
			while (it.Next(false))
			{
				if (it.propertyType != SerializedPropertyType.String && it.isArray && it.name != "Array" && it.name != "_masterWeight")
				{
					return it;
				}
			}
			Debug.LogWarning("Could not find the Converters list for " + this.name + ". Please override 'GetConvertersListProperty' in your plugin");
			return null;
		}

		/// <summary>
		/// Draws the plugins 'Help' info using the value from the plugins 'PluginHelp' property. If you override this method you will also need to override the GetPluginHeight method
		/// </summary>
		public virtual void DrawPluginHelp(Rect position)
		{
			//by default this will draw a helpbox that contains the PluginHelp string
			EditorGUI.HelpBox(position, PluginHelp, MessageType.Info);
		}
		/// <summary>
		/// Override this to draw your own content in the Elements list header
		/// Use pluginSO for to find properties to pass to standard EditorGUI.Property methods etc
		/// You may also want to override GetListHeaderHeight if you need more lines
		/// </summary>
		/// <param name="rect">The full height of the header, override GetListHeaderHeight if you need more lines sent here</param>
		/// <param name="pluginSO">The ScriptableObject representation of the plugin</param>
		/// <returns>True if you want the default elements search bar drawn, false otherwise</returns>
		public virtual bool DrawElementsListHeaderContent(Rect rect, SerializedObject pluginSO)
		{
			return true;
		}

		/// <summary>
		/// Gets an label for an entry from this plugins list of converters
		/// </summary>
		/// <param name="pluginSO">The SerializedObject representation of this plugin</param>
		/// <param name="entryIndex">The index from this plugins list of converters to draw</param>
		public virtual GUIContent GetPluginEntryLabel(SerializedProperty entry, SerializedObject pluginSO, int entryIndex)
		{
			if (entry != null)
			{
				return new GUIContent(entry.displayName);
			}
			return GUIContent.none;
		}

		/// <summary>
		/// Gets the height for an entry from this plugins list of converters.
		/// </summary>
		/// <param name="pluginSO">The SerializedObject representation of this plugin</param>
		/// <param name="entryIndex">The index from this plugins list of converters to draw</param>
		public virtual float GetPluginEntryHeight(SerializedObject pluginSO, int entryIndex, SerializedProperty entry)
		{
			if(entry != null)
			{
				if (entry.isExpanded)
					return EditorGUI.GetPropertyHeight(entry, true);
				else
					return EditorGUIUtility.singleLineHeight;
			}
			return EditorGUIUtility.singleLineHeight;
		}

		/// <summary>
		/// Draws an entry from this plugins list of converters in the UI. If you override this you may also need to override GetPluginEntryHeight.
		/// </summary>
		/// <param name="pluginSO">The SerializedObject representation of this plugin</param>
		/// <param name="entryIndex">The index from this plugins list of converters to draw</param>
		/// <param name="isExpanded">Whether the entry is currently expanded</param>
		/// <returns>whether the entry is still expanded</returns>
		public virtual bool DrawPluginEntry(Rect rect, SerializedObject pluginSO, int entryIndex, bool isExpanded, SerializedProperty entry)
		{
			if (entry != null)
			{
				EditorGUI.PropertyField(rect, entry, GetPluginEntryLabel(entry, pluginSO, entryIndex), true);
				return entry.isExpanded;
			}
			return false;
		}

		/// <summary>
		/// A callback that is called *after* a new entry is added to the plugins list of converters
		/// </summary>
		/// <param name="pluginSO">The SerializedObject representation of this plugin</param>
		/// <param name="entryIndex">The index from this plugins list of converters to that was added</param>
		public virtual void OnAddEntryCallback(SerializedObject pluginSO, int entryIndex)
		{
			//do nothing
		}

		/// <summary>
		/// A callback that is called *before* an entry will be deleted from the plugins list of converters
		/// </summary>
		/// <param name="pluginSO">The SerializedObject representation of this plugin</param>
		/// <param name="entryIndex">The index from this plugins list of converters that will be deleted/param>
		/// <returns>Returns true if the entry can safely be deleted</returns>
		public virtual bool OnRemoveEntryCallback(SerializedObject pluginSO, int entryIndex)
		{
			return true;
		}

		/// <summary>
		/// Override this to draw your own content in the Elements list footer
		/// Use pluginSO for to find properties to pass to standard EditorGUI.Property methods etc
		/// You may also want to override GetListFooterHeight if you need more lines
		/// </summary>
		/// <param name="rect">The full height of the footer, override GetListFooterHeight if you need more lines sent here</param>
		/// <param name="pluginSO">The ScriptableObject representation of the plugin</param>
		/// <returns>True if you want the default elements '+/-' add/remove controls drawn, false otherwise</returns>
		public virtual bool DrawElementsListFooterContent(Rect rect, SerializedObject pluginSO)
		{
			return true;
		}

		/// <summary>
		/// Import settings from another plugin. You need to override this method to enable this functionality in your plugin
		/// </summary>
		/// <param name="pluginToImport">The sent UnityEngine.Object. Your plugin script should first check that this plugin is the correct type</param>
		/// <returns>True if the settings imported successfully</returns>
		public virtual bool ImportSettings(UnityEngine.Object pluginToImport, int importMethod)
		{
			Debug.LogWarning("Import Settings was not implimented for this plugin");
			return false;
		}

#endif
		#endregion

		#region PRIVATE STATIC FIELDS

		private static readonly Type baseDynamicDNAPluginType = typeof(DynamicDNAPlugin);

		private static List<Type> _pluginTypes;

		#endregion

		#region PUBLIC STATIC METHODS

		public static List<Type> GetAvailablePluginTypes()
		{
			if (_pluginTypes == null)
			{
				CompilePluginTypesList();
			}
			return _pluginTypes;
		}

		public static bool IsValidPluginType(Type type)
		{
			return PluginDerivesFromBase(type);
		}

		public static bool IsValidPlugin(UnityEngine.Object asset)
		{
			try
			{
				return PluginDerivesFromBase(asset.GetType());
			}
			catch
			{
				return false;
			}
		}

		#endregion

		#region PRIVATE STATIC METHODS

		private static bool PluginDerivesFromBase(Type type)
		{
			if (type == baseDynamicDNAPluginType)
			{
				return false;
			}
			else
			{
				return baseDynamicDNAPluginType.IsAssignableFrom(type);
			}
		}

		private static void CompilePluginTypesList()
		{
			var list = new List<Type>();
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				foreach (var type in assembly.GetTypes())
				{
					if (type.IsAbstract) continue;
					if (PluginDerivesFromBase(type))
					{
						list.Add(type);
					}
				}
			}
			_pluginTypes = list;
		}

		#endregion

		#region SPECIAL TYPES

		//This override is here because I want to make sure all plugins start with a default weight of 1,
		//I also wanted to make it draw differently and have a method for applying its influence to a whole set of dna
		[System.Serializable]
		public class MasterWeight : DynamicDefaultWeight
		{
			public MasterWeight()
			{
				_defaultWeight = 1f;
			}

			public UMADnaBase GetWeightedDNA(UMADnaBase incomingDna)
			{
				var masterWeight = GetWeight(incomingDna);
				var weightedDNA = new DynamicUMADna();
				if (masterWeight > 0)
				{
					weightedDNA._names = incomingDna.Names;
					weightedDNA._values = incomingDna.Values;
					for (int i = 0; i < incomingDna.Count; i++)
					{
						weightedDNA.SetValue(i, weightedDNA.GetValue(i) * masterWeight);
					}
				}
				return weightedDNA;
			}
		}

		#endregion
	}
}
