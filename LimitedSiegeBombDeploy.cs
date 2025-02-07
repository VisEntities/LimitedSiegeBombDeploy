/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Facepunch;
using Newtonsoft.Json;
using Rust;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Limited Siege Bomb Deploy", "VisEntities", "1.0.0")]
    [Description("Stops players from placing too many siege bombs close together.")]
    public class LimitedSiegeBombDeploy : RustPlugin
    {
        #region Fields

        private static LimitedSiegeBombDeploy _plugin;
        private static Configuration _config;

        public const int LAYER_SIEGE_BOMBS = Layers.Mask.Deployed;

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Maximum Nearby Siege Bombs")]
            public int MaximumNearbySiegeBombs { get; set; }
            
            [JsonProperty("Siege Bomb Check Radius")]
            public float SiegeBombCheckRadius { get; set; }

            [JsonProperty("Siege Bomb Prefab Names")]
            public List<string> SiegeBombPrefabNames { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (string.Compare(_config.Version, Version.ToString()) < 0)
                UpdateConfig();

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void UpdateConfig()
        {
            PrintWarning("Config changes detected! Updating...");

            Configuration defaultConfig = GetDefaultConfig();

            if (string.Compare(_config.Version, "1.0.0") < 0)
                _config = defaultConfig;

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
                MaximumNearbySiegeBombs = 5,
                SiegeBombCheckRadius = 5f,
                SiegeBombPrefabNames = new List<string>
                {
                    "assets/prefabs/weapons/deployablesiegeexplosives/flammablesiegedeployable.prefab",
                    "assets/prefabs/weapons/deployablesiegeexplosives/explosivesiegedeployable.prefab"
                }
            };
        }

        #endregion Configuration

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            PermissionUtil.RegisterPermissions();
        }

        private void Unload()
        {
            _config = null;
            _plugin = null;
        }

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (planner == null || prefab == null)
                return null;

            BasePlayer player = planner.GetOwnerPlayer();
            if (player == null)
                return null;

            if (PermissionUtil.HasPermission(player, PermissionUtil.IGNORE))
                return null;

            string prefabName = prefab.fullName;
            if (!_config.SiegeBombPrefabNames.Contains(prefabName))
                return null;

            List<DeployableSiegeExplosive> nearbyBombs = GetNearbySiegeBombs(target.position, _config.SiegeBombCheckRadius);
            if (nearbyBombs.Count >= _config.MaximumNearbySiegeBombs)
            {
                MessagePlayer(player, Lang.BombDeployRestricted);
                Pool.FreeUnmanaged(ref nearbyBombs);
                return true;
            }

            Pool.FreeUnmanaged(ref nearbyBombs);
            return null;
        }

        #endregion Oxide Hooks

        #region Nearby Siege Bombs Retrieval

        private List<DeployableSiegeExplosive> GetNearbySiegeBombs(Vector3 position, float radius)
        {
            List<DeployableSiegeExplosive> siegeBombs = Pool.Get<List<DeployableSiegeExplosive>>();
            Vis.Entities(position, radius, siegeBombs, LAYER_SIEGE_BOMBS, QueryTriggerInteraction.Ignore);

            for (int i = siegeBombs.Count - 1; i >= 0; i--)
            {
                DeployableSiegeExplosive bomb = siegeBombs[i];
                if (bomb == null || !bomb.CanSee(position, bomb.ExplosionSpawnPoint.position))
                {
                    siegeBombs.RemoveAt(i);
                }
            }
            return siegeBombs;
        }

        #endregion Nearby Siege Bombs Retrieval

        #region Permissions

        private static class PermissionUtil
        {
            public const string IGNORE = "limitedsiegebombdeploy.ignore";
            private static readonly List<string> _permissions = new List<string>
            {
                IGNORE,
            };

            public static void RegisterPermissions()
            {
                foreach (var permission in _permissions)
                {
                    _plugin.permission.RegisterPermission(permission, _plugin);
                }
            }

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                return _plugin.permission.UserHasPermission(player.UserIDString, permissionName);
            }
        }

        #endregion Permissions

        #region Localization

        private class Lang
        {
            public const string BombDeployRestricted = "BombDeployRestricted";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.BombDeployRestricted] = "Too many siege bombs deployed nearby. Please move to a different location.",

            }, this, "en");
        }

        private static string GetMessage(BasePlayer player, string messageKey, params object[] args)
        {
            string message = _plugin.lang.GetMessage(messageKey, _plugin, player.UserIDString);

            if (args.Length > 0)
                message = string.Format(message, args);

            return message;
        }

        public static void MessagePlayer(BasePlayer player, string messageKey, params object[] args)
        {
            string message = GetMessage(player, messageKey, args);
            _plugin.SendReply(player, message);
        }

        public static void ShowToast(BasePlayer player, string messageKey, GameTip.Styles style = GameTip.Styles.Blue_Normal, params object[] args)
        {
            string message = GetMessage(player, messageKey, args);
            player.SendConsoleCommand("gametip.showtoast", (int)style, message);
        }

        #endregion Localization
    }
}