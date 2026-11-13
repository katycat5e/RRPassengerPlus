using Game.State;
using Model;
using Track.Data;
using UI.Menu;
using UnityEngine;

namespace PassengerPlus
{
    public class StationAgentBuilder : IMapObjectBuilder
    {
        public static void Register()
        {
            MapObjectRegistry.Register(StationAgentDTO.StationAgentKind, typeof(StationAgentDTO), new StationAgentBuilder());
        }

        public static void UnRegister()
        {
            MapObjectRegistry.Unregister(StationAgentDTO.StationAgentKind);
        }

        public bool Build(GameObject go, string id, MapObjectDTO dto)
        {
            var agentDTO = (StationAgentDTO)dto;
            if (string.IsNullOrEmpty(agentDTO.AreaId))
            {
                PassengerPlusEntry.LogWarning($"[PP] MapObjectSpawner: skipping station agent {id} with no AreaId");
                return false;
            }
            if (string.IsNullOrEmpty(agentDTO.PassengerStopId))
            {
                PassengerPlusEntry.LogWarning($"[PP] MapObjectSpawner: skipping station agent {id} with no PassengerStopId");
                return false;
            }

            CreateCollider(go);

            var agent = go.AddComponent<StationAgent>();
            agent.areaId = agentDTO.AreaId;
            agent.passengerStopId = agentDTO.PassengerStopId;

            PassengerPlusEntry.LogInfo($"[PP] Created station agent {id} with area [{agent.areaId}], stop ID [{agent.passengerStopId}]");

            bool mapEditor = StateManager.Shared.GameSceneMode == GlobalGameManager.GameSceneMode.Editor;
            PassengerPlusEntry.LogInfo($"[PP] MapEditorController: {mapEditor}");

            return true;
        }

        private static GameObject CreateCollider(GameObject agentRoot)
        {
            var colliderRoot = new GameObject();
            colliderRoot.transform.SetParent(agentRoot.transform, false);
            colliderRoot.layer = ObjectPicker.LayerClickable;

            var collider = colliderRoot.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 0, 0);
            collider.size = new Vector3(1, 1, 1);
            collider.isTrigger = true;

            return colliderRoot;
        }
    }
}