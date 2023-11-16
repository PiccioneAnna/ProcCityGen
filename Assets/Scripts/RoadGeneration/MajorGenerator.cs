using GraphModel;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#region reference
/// https://github.com/Szuszi/CityGenerator-Unity/blob/main/CityGenerator2D/Assets/Procedural%20City%20Generator/Scripts/RoadGeneration/MajorGenerator.cs
#endregion
namespace RoadGeneration
{
    public class MajorGenerator : MGenerator
    {

        #region Fields

        #endregion

        #region Constructor
        public MajorGenerator(System.Random seededRandom, int mapSize, int maxRoad, Graph graphToBuild, 
            List<Node> nodes, List<Edge> edges, float maxDegree, float branProb, float leannProb) 
            : base(seededRandom, mapSize, maxRoad, graphToBuild, nodes, edges, maxDegree, branProb, leannProb)
        {
        }
        #endregion

        #region Overridden Base Functions
        public override void Run()
        {
            GenerateStartSegments();

            SegmentGeneration();

            if (CheckMaxSegments()) { Debug.Log("Major Roads reached maximal amount"); }
        }

        public override void GenerateStartSegments()
        {
            //First Generate a number nearby the middle quarter of the map
            int sampleX = rand.Next(0, (border * 100));
            int sampleY = rand.Next(0, (border * 100));
            float starterX = (sampleX / 100.0f) - (float)border / 2;
            float starterY = (sampleY / 100.0f) - (float)border / 2;
            var startNode = new Node(starterX, starterY);

            //Secondly Generate a vector which determines the two starting directions
            int randomDirX = rand.Next(-100, 100);
            int randomDirY = rand.Next(-100, 100);
            var startDir = new Vector2(randomDirX, randomDirY);
            var starterNodeTo1 = new Node(startNode.X + startDir.normalized.x * RoadLength, starterY + startDir.normalized.y * RoadLength);
            var starterNodeTo2 = new Node(startNode.X - startDir.normalized.x * RoadLength, starterY - startDir.normalized.y * RoadLength);

            //Thirdly We make two starting RoadSegment from these
            var starterSegment1 = new Segment(startNode, starterNodeTo1);
            var starterSegment2 = new Segment(startNode, starterNodeTo2);
            queue.Enqueue(starterSegment1);
            queue.Enqueue(starterSegment2);
        }

        public override bool CheckLocalConstraints(Segment segment)
        {
            // Iterate through current segments see if any effect new road
            foreach (Segment road in segments)
            {
                //If the new segment end is close to another segments Node, Fix it's end to it
                if (IsCloseToOtherNode(segment.NodeTo, road.NodeTo))
                {
                    segment.NodeTo = road.NodeTo;
                    segment.EndSegment = true;
                }

                if (segment.IsCrossing(road)) return false; //Check if segment is crossing an other road
            }

            if (!CheckSharedConstraints(segment)) { return false; }

            // Check if node is already connected
            foreach (Edge edge in segment.NodeTo.Edges)
            {
                //NodeTo already connected to NodeFrom
                if (edge.NodeA == segment.NodeFrom || edge.NodeB == segment.NodeFrom) return false;
            }

            return true; // passes local constraints
        }

        public override void GlobalGoals(Segment segment)
        {
            if (segment.EndSegment) return;

            globalGoalsRoads.Clear();

            Branch(segment);

            globalGoalsRoads.Add(GetContinuingRoadSegment(segment));

            foreach (Segment newSegment in globalGoalsRoads)
            {
                queue.Enqueue(newSegment);
            }
        }
        #endregion

        #region Helper Functions (Child class specific)

        #endregion
    }
}

