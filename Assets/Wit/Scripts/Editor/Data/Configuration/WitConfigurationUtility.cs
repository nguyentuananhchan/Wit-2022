﻿/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

#if UNITY_EDITOR
//#define VERBOSE_LOG
#endif

using System;
using System.Collections.Generic;
using System.Threading;
using Facebook.WitAi.Windows;
using Meta.WitAi;
using Meta.WitAi.Json;
using Meta.WitAi.Data.Info;
using Meta.WitAi.Lib.Editor;
using UnityEditor;
using UnityEngine;
using Meta.Conduit;
using Meta.Conduit.Editor;

namespace Facebook.WitAi.Data.Configuration
{
    public static class WitConfigurationUtility
    {
        #region ACCESS
        // Return wit configs
        public static WitConfiguration[] WitConfigs
        {
            get
            {
                // Reload if not setup
                if (_witConfigs == null)
                {
                    ReloadConfigurationData();
                }
                // Force reload
                if (_needsConfigReload)
                {
                    ReloadConfigurationData();
                }
                // Return config data
                return _witConfigs;
            }
        }
        // Wit configuration assets
        private static WitConfiguration[] _witConfigs = null;

        // Wit configuration asset names
        public static string[] WitConfigNames => _witConfigNames;
        private static string[] _witConfigNames = Array.Empty<string>();

        // Config reload
        private static bool _needsConfigReload = false;

        // Has configuration
        public static bool HasValidCustomConfig()
        {
            // Find a valid custom configuration
            return Array.Exists(WitConfigs, (c) => !c.isDemoOnly);
        }
        // Enable config reload on next access
        public static void NeedsConfigReload()
        {
            _needsConfigReload = true;
        }
        // Refresh configuration asset list
        public static void ReloadConfigurationData()
        {
            // Reloaded
            _needsConfigReload = false;

            // Find all Wit Configurations
            List<WitConfiguration> found = new List<WitConfiguration>();
            string[] guids = AssetDatabase.FindAssets("t:WitConfiguration");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                WitConfiguration config = AssetDatabase.LoadAssetAtPath<WitConfiguration>(path);
                if (!config.isDemoOnly)
                {
                    found.Add(config);
                }
            }

            // Store wit configuration data
            _witConfigs = found.ToArray();
            // Obtain all names
            _witConfigNames = new string[_witConfigs.Length];
            for (int i = 0; i < _witConfigs.Length; i++)
            {
                _witConfigNames[i] = _witConfigs[i].name;
            }
        }
        // Get configuration index
        public static int GetConfigurationIndex(WitConfiguration configuration)
        {
            // Search through configs
            return Array.FindIndex(WitConfigs, (checkConfig) => checkConfig == configuration );
        }
        // Get configuration index
        public static int GetConfigurationIndex(string configurationName)
        {
            // Search through configs
            return Array.FindIndex(WitConfigs, (checkConfig) => string.Equals(checkConfig.name, configurationName));
        }
        #endregion

        #region MANAGEMENT
        // Create configuration for token with blank configuration
        public static int CreateConfiguration(string serverToken)
        {
            // Generate blank asset
            WitConfiguration configurationAsset = ScriptableObject.CreateInstance<WitConfiguration>();
            configurationAsset.name = WitTexts.Texts.ConfigurationFileNameLabel;
            configurationAsset.ResetData();
            // Create
            int index = SaveConfiguration(serverToken, configurationAsset);
            if (index == -1)
            {
                MonoBehaviour.DestroyImmediate(configurationAsset);
            }
            // Return new index
            return index;
        }
        // Get asset save directory
        public static string GetFileSaveDirectory(string title, string fileName, string fileExt)
        {
            // Determine root directory with selection if possible
            string rootDirectory = Application.dataPath;
            if (Selection.activeObject)
            {
                // Get asset path
                string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                // Only allow if in assets
                if (selectedPath.StartsWith("Assets"))
                {
                    if (AssetDatabase.IsValidFolder(selectedPath))
                    {
                        rootDirectory = selectedPath;
                    }
                    else if (!string.IsNullOrEmpty(selectedPath))
                    {
                        rootDirectory = new System.IO.FileInfo(selectedPath).DirectoryName;
                    }
                }
            }
            // Save panel
            return EditorUtility.SaveFilePanel(title, rootDirectory, fileName, fileExt);
        }
        // Save configuration after determining path
        public static int SaveConfiguration(string serverToken, WitConfiguration configurationAsset)
        {
            string savePath = GetFileSaveDirectory(WitTexts.Texts.ConfigurationFileManagerLabel, WitTexts.Texts.ConfigurationFileNameLabel, "asset");
            return SaveConfiguration(savePath, serverToken, configurationAsset);
        }
        // Save configuration to selected location
        public static int SaveConfiguration(string savePath, string serverToken, WitConfiguration configurationAsset)
        {
            // Ensure valid save path
            if (string.IsNullOrEmpty(savePath))
            {
                return -1;
            }
            // Must be in assets
            string unityPath = savePath.Replace("\\", "/");
            if (!unityPath.StartsWith(Application.dataPath))
            {
                VLog.E($"Configuration Utility - Cannot Create Configuration Outside of Assets Directory\nPath: {unityPath}");
                return -1;
            }

            // Determine local unity path
            unityPath = unityPath.Replace(Application.dataPath, "Assets");
            AssetDatabase.CreateAsset(configurationAsset, unityPath);
            AssetDatabase.SaveAssets();

            // Refresh configurations
            ReloadConfigurationData();

            // Get new index following reload
            string name = System.IO.Path.GetFileNameWithoutExtension(unityPath);
            int index = GetConfigurationIndex(name);

            // Set server token
            if (!string.IsNullOrEmpty(serverToken))
            {
                _witConfigs[index].SetServerToken(serverToken);
            }

            // Return index
            return index;
        }
        #endregion

        #region TOKENS
        // Token valid check
        public static bool IsServerTokenValid(string serverToken)
        {
            return !string.IsNullOrEmpty(serverToken) && WitAuthUtility.IsServerTokenValid(serverToken);
        }
        // Token valid check
        public static bool IsClientTokenValid(string clientToken)
        {
            return !string.IsNullOrEmpty(clientToken) && clientToken.Length == 32;
        }
        // Sets server token for all configurations if possible
        public static void SetServerToken(string serverToken, Action<string> onSetComplete = null)
        {
            // Invalid token
            if (!IsServerTokenValid(serverToken))
            {
                SetServerTokenComplete(string.Empty, "", onSetComplete);
                return;
            }
            // Perform a list app request to get app for token
            WitAppInfoUtility.GetAppInfo(serverToken, (clientToken, info, error) =>
            {
                SetServerTokenComplete(serverToken, error, onSetComplete);
            });
        }
        // Set server token complete
        private static void SetServerTokenComplete(string serverToken, string error, Action<string> onSetComplete)
        {
            // Failed
            if (!string.IsNullOrEmpty(error))
            {
                VLog.E($"Set Server Token Failed\n{error}");
                WitAuthUtility.ServerToken = "";
            }
            // Success
            else
            {
                // Log Success
                VLog.D("Set Server Token Success");
                // Apply token
                WitAuthUtility.ServerToken = serverToken;
                // Refresh configurations
                ReloadConfigurationData();
            }
            // On complete
            onSetComplete?.Invoke(error);
        }
        // Sets server token for specified configuration by updating it's application data
        public static void SetServerToken(this WitConfiguration configuration, string serverToken, Action<string> onSetComplete = null)
        {
            // Invalid
            if (!IsServerTokenValid(serverToken))
            {
                onSetComplete?.Invoke("Invalid Token");
                return;
            }
            // Perform a list app request to get app for token
            WitAppInfoUtility.GetAppInfo(serverToken, (clientToken, info, error) =>
            {
                // Set client access token
                configuration.SetClientAccessToken(clientToken);
                // Set application info
                configuration.SetApplicationInfo(info);
                // Set server token
                if (!string.IsNullOrEmpty(info.id))
                {
                    WitAuthUtility.SetAppServerToken(info.id, serverToken);
                }
                // Complete
                onSetComplete?.Invoke(error);
            });
        }
        #endregion

        #region APP DATA IMPORT

        /// <summary>
        /// Note: this method could be quite slow as it uses multi-request approach.
        ///
        /// Use this method to import data via multiple requests:
        /// - First batch of requests to prepare Intents
        /// - Last request to actually import the manifest itself
        /// </summary>
        internal static void ImportData(this WitConfiguration configuration, Manifest manifest, List<string> intents) {
            var manifestData = GetSanitizedManifestString(manifest);
            
            int intentRequestsLeft = intents.Count;

            Action proceedWithDataImport = () => {
                VLog.D("Calling POST /import");
                var req = WitRequestFactory.ImportData(configuration, GetAppName(configuration), manifestData);
                PerformRequest(req, (error) =>
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        VLog.E($"Failed to import generated manifest JSON into WIT.ai: {error}.");
                    }
                    else
                    {
                        VLog.D("Successfully imported generated manifest JSON into WIT.ai.");
                        EditorUtility.DisplayDialog("Auto Train", "Successfully started auto train process on WIT.ai.", "OK");
                    }
                });
            };
            Action reqHandler = () => {
                if (intentRequestsLeft <= 0) {
                    proceedWithDataImport();
                }
            };

            foreach (string intentName in intents) {
                var req = WitRequestFactory.PostIntentRequest(configuration, intentName);
                PerformRequest(req, (error) => {
                    Interlocked.Decrement(ref intentRequestsLeft);
                    reqHandler();
                });
            }
        }

        /// <summary>
        /// Returns a serialized version of the manifest after removing internal data that should not be sent to IDM.
        /// </summary>
        /// <param name="manifest">The manifest to process.</param>
        private static string GetSanitizedManifestString(Manifest manifest)
        {
            var filter = new WitParameterFilter();
            foreach (var action in manifest.Actions)
            {
                action.Parameters.RemoveAll(a => filter.ShouldFilterOut(a.EntityType));
            }

            return JsonConvert.SerializeObject(manifest);
        }

        /// <summary>
        /// Use this method to import data via one /import request.
        /// Make sure API endpoint supports this.
        /// </summary>
        internal static void ImportData(this WitConfiguration configuration, Manifest manifest)
        {
            var manifestData = GetSanitizedManifestString(manifest);
            
            var request = configuration.ImportData(GetAppName(configuration), manifestData);
            PerformRequest(request, (error) =>
            {
                if (!string.IsNullOrEmpty(error))
                {
                    VLog.E($"Failed to import generated manifest JSON into WIT.ai: {error}. Manifest:\n{manifestData}");
                }
                else
                {
                    VLog.D("Successfully imported generated manifest JSON into WIT.ai.");
                    EditorUtility.DisplayDialog("Auto Train", "Successfully started auto train process on WIT.ai.", "OK");
                }
            });
        }
        #endregion

        #region REFRESH
        // Refresh if possible & return true if still refreshing
        private static List<string> refreshAppIDs = new List<string>();
        // Check if refreshing
        private static bool IsRefreshing(string appID)
        {
            return !string.IsNullOrEmpty(appID) && refreshAppIDs.Contains(appID);
        }
        // Check if refreshing
        public static bool IsRefreshingData(this WitConfiguration configuration)
        {
            string appID = configuration.GetApplicationId();
            return IsRefreshing(appID);
        }
        // Refreshes configuration data
        public static void RefreshAppInfo(this WitConfiguration configuration, Action<string> onRefreshComplete = null)
        {
            // Ignore during runtime
            if (Application.isPlaying)
            {
                onRefreshComplete?.Invoke(null);
                return;
            }

            // Update application info
            WitAppInfoUtility.Update(configuration, (info, s) =>
            {
                // Set application info
                configuration.SetApplicationInfo(info);

                // Complete
                onRefreshComplete?.Invoke(s);
            });
        }

        // Get application name
        private static string GetAppName(WitConfiguration configuration)
        {
            if (configuration != null)
            {
                return configuration.GetApplicationInfo().name;
            }
            return string.Empty;
        }

        private static void PerformRequest(WitRequest request, Action<string> onComplete)
        {
            // Add response delegate
            request.onResponse += (response) =>
            {
                // Get status
                int status = response.StatusCode;
                // HTTP failed
                if (status != 200)
                {
                    onComplete($"Request Failed [{status}]: {response.StatusDescription}\nPath: {request}");
                }
                // Success
                else
                {
                    VLog.D($"Request Success\nType: {request}");
                    onComplete?.Invoke("");
                }
            };

            // Perform
            VLog.D($"Request Begin\nType: {request}");
            request.Request();
        }
        #endregion
    }
}
