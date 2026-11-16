using System;
using System.Collections.Generic;
using System.Linq;
using Game.MapEditor;
using Model;
using Model.Ops;
using Model.Ops.Data;
using Track;
using Track.Search;
using UnityEngine;

namespace PassengerPlus
{
    internal static class TimetableConstruction
    {
        public static void CreateTimetable(MapEditor mapEditor)
        {
            if (mapEditor.OpsEditor.Ops.timetable != null) return;

            var mapDTO = new TimetableMapDTO();
            mapEditor.PerformUndoable("Create Timetable", () =>
            {
                OpsAddTimetable(mapEditor.OpsEditor, mapDTO);
            });
            mapEditor.NotifyItemCreated();
        }

        private static void OpsAddTimetable(OpsEditor opsEditor, TimetableMapDTO mapDTO)
        {
            opsEditor.Ops.timetable = mapDTO;
            opsEditor.UndoManager.RegisterUndo(() =>
            {
                opsEditor.Ops.timetable = null;
            }, "Add Timetable");
            opsEditor.NotifyChange();
        }

        public static void CreateTimetableBranch(MapEditor mapEditor)
        {
            if (mapEditor.OpsEditor.Ops.timetable is null) return;

            var branchDTO = new TimetableBranchDTO();
            mapEditor.PerformUndoable("Create Timetable Branch", () =>
            {
                OpsAddTimetableBranch(mapEditor.OpsEditor, branchDTO);
            });
        }

        public static void CreateTimetableBranch(MapEditor mapEditor, string easternTerminus, string westernTerminus)
        {
            if (mapEditor.OpsEditor.Ops.timetable is null) return;

            var branchDTO = BuildTimetableBranch(mapEditor, easternTerminus, westernTerminus);
            if (branchDTO is null) return;

            mapEditor.PerformUndoable("Create Timetable Branch", () =>
            {
                OpsAddTimetableBranch(mapEditor.OpsEditor, branchDTO);
            });
        }

        private static void OpsAddTimetableBranch(OpsEditor opsEditor, TimetableBranchDTO branchDTO)
        {
            var timetable = opsEditor.Ops.timetable;
            if (timetable.branches is null) timetable.branches = new List<TimetableBranchDTO>();

            timetable.branches.Add(branchDTO);
            opsEditor.UndoManager.RegisterUndo(() =>
            {
                timetable.branches?.Remove(branchDTO);
            }, "Add Timetable Branch");
            opsEditor.NotifyChange();
        }

        private static TimetableBranchDTO BuildTimetableBranch(MapEditor mapEditor, string easternTerminus, string westerTerminus)
        {
            var eastStop = EditorHelpers.GetStopById(mapEditor, easternTerminus);
            var westStop = EditorHelpers.GetStopById(mapEditor, westerTerminus);

            if (eastStop is null || westStop is null)
            {
                PassengerPlusEntry.LogWarning($"[PP] Couldn't find terminal passenger stops: {easternTerminus}, {westerTerminus}");
                return null;
            }

            var route = FindTimetableRoute(mapEditor, eastStop, westStop);
            if (route is null) return null;

            var branch = new TimetableBranchDTO();
            TimetableStationDTO previousStation = null;

            foreach (var edge in route)
            {
                if (previousStation != null)
                {
                    previousStation.traverseTimeToNext = edge.TimeFromPrevious;
                }

                var currentStation = DtoForStop(edge.EndNode.StopId, edge.EndNode.Stop);
                branch.stations.Add(currentStation);
                previousStation = currentStation;
            }

            return branch;
        }

        private static TimetableStationDTO DtoForStop(string stopId, PassengerStopDTO stop)
        {
            return new TimetableStationDTO()
            {
                code = stop.timetableCode,
                name = stop.name,
                passengerStopId = stopId,
            };
        }

        private const float TRAVEL_TIME_SCALE_FACTOR = 1.5f;
        private const float ACCEL_DECEL_SECONDS = 60f;

        private static List<TimetableEdge> FindTimetableRoute(MapEditor mapEditor, PassengerStopDTO east, PassengerStopDTO west)
        {
            var stopMap = new Dictionary<string, TimetableNode>();
            var eastLocations = new List<TimetableNode>(1);
            var westLocations = new List<TimetableNode>(1);

            foreach (var area in mapEditor.OpsEditor.Ops.areas)
            {
                foreach (var industry in area.Value.industries)
                {
                    foreach (var component in industry.Value.components)
                    {
                        if (component.Value is PassengerStopDTO stop)
                        {
                            if (stop == east)
                            {
                                FetchStopLocations(mapEditor, industry.Value, component.Key, stop, stopMap, eastLocations);
                            }
                            else if (stop == west)
                            {
                                FetchStopLocations(mapEditor, industry.Value, component.Key, stop, stopMap, westLocations);
                            }
                            else
                            {
                                FetchStopLocations(mapEditor, industry.Value, component.Key, stop, stopMap);
                            }
                        }
                    }
                }
            }

            var route = GetTimetableRoute(mapEditor.GraphEditor.Graph, eastLocations, westLocations);

            if (route is null)
            {
                PassengerPlusEntry.LogWarning($"[PP] Failed to determine route from {east.name} to {west.name}");
                return null;
            }

            var edges = new List<TimetableEdge>();
            float currentLegDuration = 0;
            for (int i = 0; i < route.Count; i++)
            {
                var step = route[i];
                PassengerPlusEntry.LogInfo($"[PP] Route Step: {step.Distance:F1}m ({step.Location.segment}) [{step.Node}]");

                float speedLimitMPS = step.Location.segment.EffectiveSpeedLimitMph * Units.MphToMps;
                float stepDuration = step.Distance / speedLimitMPS; 

                if (stopMap.TryGetValue(step.Location.segment.id, out TimetableNode passedNode) && !edges.Any(e => e.EndNode == passedNode))
                {
                    int edgeDuration = Mathf.CeilToInt((currentLegDuration * TRAVEL_TIME_SCALE_FACTOR) + ACCEL_DECEL_SECONDS);
                    edges.Add(new TimetableEdge(passedNode, edgeDuration));
                    PassengerPlusEntry.LogInfo($"[PP] Add stop {passedNode.StopId}: T+{edgeDuration} sec");
                    currentLegDuration = 0;
                }
                
                currentLegDuration += stepDuration;

                PassengerPlusEntry.LogInfo($"[PP] Leg Duration: {currentLegDuration:F1}s");
            }

            return edges;
        }
        
        private static List<RouteSearch.Step> GetTimetableRoute(Graph graph, IEnumerable<TimetableNode> start, IEnumerable<TimetableNode> end)
        {
            List<RouteSearch.Step> result;
            foreach (var startNode in start)
            {
                foreach (var endNode in end)
                {
                    result = graph.FindRoute(startNode.Location, endNode.Location, HeuristicCosts.Zero);
                    if (result != null) return result;
                }
            }
            return null;
        }

        private static void FetchStopLocations(MapEditor mapEditor, IndustryDTO parentIndustry, string stopId, PassengerStopDTO stop, IDictionary<string, TimetableNode> stopMap, ICollection<TimetableNode> nodes = null)
        {
            foreach (string spanId in stop.trackSpans)
            {
                if (parentIndustry.trackSpans.TryGetValue(spanId, out var spanDTO) &&
                    TryGetSpanMidpoint(mapEditor, spanDTO, out Location midPoint))
                {
                    var node = new TimetableNode(midPoint, stopId, stop);
                    stopMap[midPoint.segment.id] = node;
                    nodes?.Add(node);
                }
            }
        }
        
        private static bool TryGetSpanMidpoint(MapEditor mapEditor, TrackSpanDTO span, out Location midPoint)
        {
            if (TrackSpanReferences.TryResolveLocation(mapEditor.GraphEditor.Graph, span.lower, out Location lower) &&
                TrackSpanReferences.TryResolveLocation(mapEditor.GraphEditor.Graph, span.upper, out Location upper))
            {
                midPoint = mapEditor.GraphEditor.Graph.Lerp(lower, upper, 0.5f);
                return true;
            }
            midPoint = default;
            return false;
        }

        private readonly struct TimetableEdge
        {
            public readonly TimetableNode EndNode;
            public readonly int TimeFromPrevious;

            public TimetableEdge(TimetableNode endNode, int travelTime)
            {
                EndNode = endNode;
                TimeFromPrevious = travelTime;
            }
        }

        private readonly struct TimetableNode
        {
            public readonly Location Location;
            public readonly string StopId;
            public readonly PassengerStopDTO Stop;

            public TimetableNode(Location location, string stopId, PassengerStopDTO stop)
            {
                Location = location;
                StopId = stopId;
                Stop = stop;
            }

            public override bool Equals(object obj)
            {
                return (obj is TimetableNode other) && (StopId == other.StopId);
            }

            public static bool operator ==(TimetableNode a, TimetableNode b)
            {
                return a.StopId == b.StopId;
            }

            public static bool operator !=(TimetableNode a, TimetableNode b)
            {
                return a.StopId != b.StopId;
            }

            public override int GetHashCode()
            {
                return StopId.GetHashCode();
            }
        }
    }
}