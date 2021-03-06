﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace TimeCells
{
    public class TimeCanvas
    {
        public delegate double DistanceFunction(double[] a, double[] b);

        public double parameterLineCreationTreshold = 1.0;
        public double parameterFFLearningRate = 0.01;

        private int timeLineSize;
        private int inputSize;

        private List<TimeLine> lines = new List<TimeLine>();
        
        public DistanceFunction distanceFx;

        public delegate void DelegateProcessElement(double[] elt);
        public delegate void DelegateProcessSequence(List<double[]> seq);
        public event DelegateProcessElement onElementTraining;
        public event DelegateProcessElement onElementPresented;
        public event DelegateProcessSequence onSequenceTraining;
        public event DelegateProcessSequence onSequencePresented;


        #region Goal Autoassociator
        private List<Dictionary<CDZNET.Core.Signal, double[,]>> goalTrainingElements = new List<Dictionary<CDZNET.Core.Signal, double[,]>>();
        private CDZNET.Core.MMNode goalNetwork;

        private List<KeyValuePair<double[,], double[,]>> goalTrainingElementsIO = new List<KeyValuePair<double[,], double[,]>>();
        private CDZNET.Core.IONode goalNetworkIO;      

        public void EnableGoal()
        {
            onElementTraining += CollectGoalTrainingElement;
            //goalNetwork = new CDZNET.Core.MMNodeDeepBeliefNetwork(new CDZNET.Point2D(1, 1), new int[] { 20, 10, 5 });
            goalNetwork = new CDZNET.Core.MMNodeMLP(new CDZNET.Point2D(1, 1), new int[] { 10, 5 });
            //goalNetwork = new CDZNET.Core.MMNodeMWSOM(new CDZNET.Point2D(50, 50));
            //goalNetwork = new CDZNET.Core.MMNodeLookupTable(new CDZNET.Point2D(1,1));
            goalNetwork.addModality(new CDZNET.Core.Signal(lines.Count, 3)); //0 - current state //1 - next state //2 - goal

            goalNetworkIO = new CDZNET.Core.IONodeAFMLP(
                new CDZNET.Point2D(lines.Count, 2), //Input
                new CDZNET.Point2D(lines.Count, 1), //Output
                new int[] { 30, 10 },               //bottomup layers
                new int[] { 1 });              //topdown layers

            (goalNetworkIO as CDZNET.Core.IONodeAFMLP).skipTopDown = true;

            //Messages
            goalNetwork.onEpoch += goalNetwork_onEpoch;
            goalNetworkIO.onEpoch += goalNetworkIO_onEpoch;
        }

        void goalNetwork_onEpoch(int currentEpoch, int maximumEpoch, Dictionary<CDZNET.Core.Signal, double> modalitiesMSE, double MSE)
        {
            Console.WriteLine("AutoAsso Epoch " + currentEpoch + " / " + maximumEpoch + "\t Error = " + MSE);
        }

        void goalNetworkIO_onEpoch(int currentEpoch, int maximumEpoch, double outputMaxError, double inputMaxError)
        {
            //if (currentEpoch % (maximumEpoch/100.0) == 0)
                Console.WriteLine("Hetero Epoch " + currentEpoch + " / " + maximumEpoch + "\t Error = " + outputMaxError);
        }

        public void TrainGoalNetwork()
        {
            DisplayTrainingSetGoal();
            Console.WriteLine("Training GoalNet...");
            //goalNetwork.learningLocked = false;
            //int totalCycle2 = goalNetwork.Batch(goalTrainingElements, 5000, 0.5);
            //Console.WriteLine("Key?"); Console.ReadKey();
            int totalCycle = goalNetworkIO.Batch(goalTrainingElementsIO, 5000, 0.5);
            Console.WriteLine("Key?"); Console.ReadKey();
            goalNetworkIO.learningLocked = true;
            goalNetwork.learningLocked = true;
            
            //goalNetwork.learningLocked = true;
            Console.WriteLine("Achieved in " + totalCycle);
        }

        public void DisplayTrainingSetGoal()
        {
            Console.WriteLine("TRAINING SET");
            foreach(Dictionary<CDZNET.Core.Signal, double[,]> e in goalTrainingElements)
            {
                double[,] p = e[goalNetwork.modalities.First()];
                Console.WriteLine(tripletToString(p));
            }
        }

        public int findMaxIndex(double[] distribution)
        {
            int bestIndex = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                if (distribution[i] > distribution[bestIndex])
                    bestIndex = i;
            }
            return bestIndex; 
        }

        public string tripletToString(double[,] triplet)
        {
            double[] s = new double[lines.Count];
            double[] s1 = new double[lines.Count];
            double[] g = new double[lines.Count];
            for (int i = 0; i < lines.Count; i++)
            {
                s[i] = triplet[i, 0];
                s1[i] = triplet[i, 1];
                g[i] = triplet[i, 2];
            }
            TimeLine tlS = lines[findMaxIndex(s)];
            TimeLine tlS1 = lines[findMaxIndex(s1)];
            TimeLine tlG = lines[findMaxIndex(g)];
            return ("(" + DEBUGConvert(tlS.receptiveField) + "," + DEBUGConvert(tlS1.receptiveField) + "," + DEBUGConvert(tlG.receptiveField) + ")");
        }

        public string tripletToString(double[,] stateGoal, double[,] nextAction)
        {
            double[] s = new double[lines.Count];
            double[] s1 = new double[lines.Count];
            double[] g = new double[lines.Count];
            for (int i = 0; i < lines.Count; i++)
            {
                s[i] = stateGoal[i, 0];
                s1[i] = stateGoal[i, 1];
                g[i] = nextAction[i, 0];
            }
            TimeLine tlS = lines[findMaxIndex(s)];
            TimeLine tlS1 = lines[findMaxIndex(s1)];
            TimeLine tlG = lines[findMaxIndex(g)];
            return ("(" + DEBUGConvert(tlS.receptiveField) + "," + DEBUGConvert(tlS1.receptiveField) + "," + DEBUGConvert(tlG.receptiveField) + ")");
        }

        public void CollectGoalTrainingElement(double[] input)
        {
            for (int level = 0; level < timeLineSize-1; level++)
            {
                double[,] triplet = new double[lines.Count, 3]; //0 - current state //1 - next state //2 - goal

                //Set the initial state
                bool isInitialEmpty = true;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].cells[level+1].isActive)
                    {
                        triplet[i, 0] = 1.0;
                        isInitialEmpty = false;
                    }
                    else
                        triplet[i, 0] = 0.0;
                }

                //Set the next state
                bool isNextEmpty = true;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].cells[level].isActive)
                    {
                        triplet[i, 1] = 1.0;
                        isNextEmpty = false;
                    }
                    else
                        triplet[i, 1] = 0.0;
                }

                //Set the goal
                bool isGoalEmpty = true;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].cells[0].isActive)
                    {
                        triplet[i, 2] = 1.0;
                        isGoalEmpty = false;
                    }
                    else
                        triplet[i, 2] = 0.0;
                }

                if (isInitialEmpty || isNextEmpty || isGoalEmpty)
                    break;
                else
                {
                    Dictionary<CDZNET.Core.Signal, double[,]> newDic = new Dictionary<CDZNET.Core.Signal, double[,]>();
                    newDic.Add(goalNetwork.modalities.First(), triplet);
                    goalTrainingElements.Add(newDic);
                    Console.WriteLine(tripletToString(triplet));


                    double[,] tripletInput = new double[lines.Count,2];
                    double[,] tripletOutput = new double[lines.Count,1];
                    for (int i = 0; i < lines.Count; i++)
                    {
                        tripletInput[i,0] = triplet[i,0]; //initial state
                        tripletInput[i,1] = triplet[i,2]; //goal
                        tripletOutput[i,0] = triplet[i,1]; //next step
                    }
                    goalTrainingElementsIO.Add(new KeyValuePair<double[,], double[,]>(tripletInput, tripletOutput));
                }
            }
        }

        public bool findPathGoalNetwork(double[] start, double[] end, out List<TimeLine> path)
        {
            path = new List<TimeLine>();

            TimeLine bestLineStart = null;
            double bestDistanceStart = double.PositiveInfinity;
            bool hasGoodTimelineStart = FindBestLine(start, out bestLineStart, out bestDistanceStart);

            TimeLine bestLineEnd = null;
            double bestDistanceEnd = double.PositiveInfinity;
            bool hasGoodTimelineEnd = FindBestLine(end, out bestLineEnd, out bestDistanceEnd);

            //If we do not have good representation for the start or the end we give up.
            //We could also take the closest ones...
            if (!(hasGoodTimelineStart && hasGoodTimelineEnd))
                return false;

            int indexStart = lines.IndexOf(bestLineStart);
            int indexEnd = lines.IndexOf(bestLineEnd);
            path.Add(lines[indexStart]);

            while (indexStart != indexEnd)
            {
                double[,] triplet = new double[lines.Count, 3]; //0 - current state //1 - next state //2 - goal
                triplet[indexStart, 0] = 1.0;
                triplet[indexEnd, 2] = 1.0;
                Console.WriteLine("Presenting triplet " + tripletToString(triplet));
                goalNetwork.modalities.First().reality = triplet.Clone() as double[,];
                goalNetwork.Converge();
                goalNetwork.Diverge();
                double[,] pred = ca1Network.modalities.First().prediction.Clone() as double[,];
                //Find the best next element
                int bestIndex = 0;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (pred[i, 1] > pred[bestIndex, 1])
                        bestIndex = i;
                }

                indexStart = bestIndex;
                //If we came back to a state that is already in the path we just give up
                if (path.Contains(lines[indexStart]))
                    return false;
                path.Add(lines[bestIndex]);
            }
            return (indexStart == indexEnd);
        }

        public bool findPathGoalNetworkIO(double[] start, double[] end, out List<TimeLine> path)
        {
            Console.WriteLine("--> [findPath] Looking for a path from " + DEBUGConvert(start) + " to " + DEBUGConvert(end));
            path = new List<TimeLine>();

            TimeLine bestLineStart = null;
            double bestDistanceStart = double.PositiveInfinity;
            bool hasGoodTimelineStart = FindBestLine(start, out bestLineStart, out bestDistanceStart);

            TimeLine bestLineEnd = null;
            double bestDistanceEnd = double.PositiveInfinity;
            bool hasGoodTimelineEnd = FindBestLine(end, out bestLineEnd, out bestDistanceEnd);

            //If we do not have good representation for the start or the end we give up.
            //We could also take the closest ones...
            if (!(hasGoodTimelineStart && hasGoodTimelineEnd))
                return false;

            int indexStart = lines.IndexOf(bestLineStart);
            int indexEnd = lines.IndexOf(bestLineEnd);
            path.Add(lines[indexStart]);

            int loopingCount = 0;
            while (indexStart != indexEnd)
            {
                double[,] tripletInput = new double[lines.Count, 2]; //0 - current state //1 - goal
                tripletInput[indexStart, 0] = 1.0;
                tripletInput[indexEnd, 1] = 1.0;

                goalNetworkIO.input.reality = tripletInput.Clone() as double[,];
                goalNetworkIO.BottomUp();
                double[,] pred = goalNetworkIO.output.prediction.Clone() as double[,];
                //Find the best next element
                int bestIndex = 0;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (pred[i, 0] > pred[bestIndex, 0])
                        bestIndex = i;
                }
                //Console.WriteLine("Presenting triplet " + tripletToString(tripletInput, pred));

                //If the next element is invlaid, we treat it as a subgoal
                TimeLine subStart = lines[indexStart];
                TimeLine subEnd = lines[bestIndex];
                if (!IsAccessible(subStart, subEnd) /*lines[indexStart].cells[1].next.ContainsKey(lines[bestIndex].cells[0])*/)
                {
                    Console.WriteLine("[Illegal move]");
                    if (subStart.receptiveField == start && subEnd.receptiveField == end)
                    {
                        Console.WriteLine("ERROR Subgoal == endgoal. Network thinks he can reach in one step...");
                        return false;
                    }
                    List<TimeLine> subPath;
                    bool successSubpath = findPathGoalNetworkIO(subStart.receptiveField, subEnd.receptiveField, out subPath);   
                    path.AddRange(subPath);
                    if (!successSubpath)
                        return false;
                    indexStart = lines.IndexOf(path.Last());
                }
                else
                {
                    indexStart = bestIndex;

                    //If we came back to a state that is already in the path we just give up
                    if (path.Contains(lines[indexStart]))
                    {
                        loopingCount++;
                        if (loopingCount > 3)
                        {
                            path.Add(lines[bestIndex]);
                            Console.WriteLine("Abort due to circular path");
                            return false;
                        }
                    }
                }
                path.Add(lines[bestIndex]);
            }
            return (indexStart == indexEnd);
        }

        bool IsAccessible(TimeLine a, TimeLine b)
        {
            foreach (TimeCell c in b.cells[0].previous.Keys)
            {
                if (c.parentLine == a)
                {
                    return true;
                }
            }
            return false;
        }

        #endregion
        #region CA1 Autoassociator

        private List<Dictionary<CDZNET.Core.Signal, double[,]>> ca1TrainingElements = new List<Dictionary<CDZNET.Core.Signal, double[,]>>();
        private CDZNET.Core.MMNodeAFSOM ca1Network;

        public void EnableCA1()
        {
            onElementTraining += CollectTrainingElement;
            ca1Network = new CDZNET.Core.MMNodeAFSOM(new CDZNET.Point2D(10, 10));
            ca1Network.addModality(new CDZNET.Core.Signal(lines.Count, timeLineSize));
        }

        public void TrainCA1()
        {
            Console.WriteLine("Training CA1...");
            ca1Network.learningLocked = false;
            int totalCycle = ca1Network.Batch(ca1TrainingElements, 50000, 0.01);
            ca1Network.learningLocked = true;
            Console.WriteLine("Achieved in " + totalCycle);
        }

        public void CollectTrainingElement(double[] input)
        {
            Dictionary<CDZNET.Core.Signal, double[,]> newDic = new Dictionary<CDZNET.Core.Signal,double[,]>();
            newDic.Add(ca1Network.modalities.First(), GetActivitySnapshot());
            ca1TrainingElements.Add(newDic);
        }

        public  double[,] GetActivitySnapshot()
        {
            double[,] ss = new double[lines.Count, timeLineSize];
            int i =0, j = 0;
            foreach(TimeLine l in lines)
            {
                j = 0;
                foreach(TimeCell c in l.cells)
                {
                    ss[i, j] = (c.isActive) ? 1.0 : 0.0;
                    j++;
                }
                i++;
            }
            return ss;
        }
        public List<KeyValuePair<double[], double>> PredictAllCA1()
        {
            double[,] ss = GetActivitySnapshot();
            ca1Network.modalities.First().reality = ss;
            ca1Network.Converge();
            ca1Network.Diverge();
            double[,] pred = ca1Network.modalities.First().prediction;

            List<KeyValuePair<double[], double>> predictions = new List<KeyValuePair<double[], double>>();
            int i = 0;
            foreach (TimeLine line in lines)
            {
                if (pred[i, 0]>0.0)
                    predictions.Add(new KeyValuePair<double[], double>(line.receptiveField, pred[i,0]));
                i++;
            }
            predictions.Sort((a, b) => a.Value.CompareTo(b.Value));
            predictions.Reverse();
            return predictions;
        }

        public bool findPathCA1(double[] start, double[] end, out List<TimeLine> path)
        {
            path = new List<TimeLine>();
            
            TimeLine bestLineStart = null;
            double bestDistanceStart = double.PositiveInfinity;
            bool hasGoodTimelineStart = FindBestLine(start, out bestLineStart, out bestDistanceStart);

            TimeLine bestLineEnd = null;
            double bestDistanceEnd = double.PositiveInfinity;
            bool hasGoodTimelineEnd = FindBestLine(end, out bestLineEnd, out bestDistanceEnd);

            //If we do not have good representation for the start or the end we give up.
            //We could also take the closest ones...
            if (!(hasGoodTimelineStart && hasGoodTimelineEnd))
                return false;

            int indexStart = lines.IndexOf(bestLineStart);
            int indexEnd = lines.IndexOf(bestLineEnd);
            List<int> pathIndex = new List<int>();

            path.Add(lines[indexStart]);
            pathIndex.Add(indexStart);

            while (indexStart!=indexEnd)
            {
                //Clear the array
                Array.Clear(ca1Network.modalities.First().reality, 0, ca1Network.modalities.First().reality.Length);
                //Add the current state
                for (int i = 1; i < timeLineSize && i<=pathIndex.Count; i++)
                {
                    ca1Network.modalities.First().reality[pathIndex[pathIndex.Count - i], i] = 1;          
                }
                //Add the goal as the "next" element
                ca1Network.modalities.First().reality[indexEnd,0] = 0.75;//make it weaker than the current state to avoid "backtraking" from there
                
                //Predict
                ca1Network.Converge();
                ca1Network.Diverge();
                double[,] pred = ca1Network.modalities.First().prediction;

                //Find the best next element
                int bestIndex = 0;
                for (int i = 0; i < lines.Count; i++)
			    {
                    if (pred[i,0]>pred[bestIndex,0])
                        bestIndex = i;
                }
               
                //Take the first element as the next one
                indexStart = bestIndex;

                //If we came back to a state that is already in the path we just give up
                if (path.Contains(lines[indexStart]))
                    return false;

                //Add the current state to the path
                path.Add(lines[indexStart]);
                pathIndex.Add(indexStart);
            }
            return (indexStart == indexEnd);
        }

        public bool findPathCA12(double[] start, double[] end, out List<TimeLine> path)
        {
            path = new List<TimeLine>();

            TimeLine bestLineStart = null;
            double bestDistanceStart = double.PositiveInfinity;
            bool hasGoodTimelineStart = FindBestLine(start, out bestLineStart, out bestDistanceStart);

            TimeLine bestLineEnd = null;
            double bestDistanceEnd = double.PositiveInfinity;
            bool hasGoodTimelineEnd = FindBestLine(end, out bestLineEnd, out bestDistanceEnd);

            //If we do not have good representation for the start or the end we give up.
            //We could also take the closest ones...
            if (!(hasGoodTimelineStart && hasGoodTimelineEnd))
                return false;

            int indexStart = lines.IndexOf(bestLineStart);
            int indexEnd = lines.IndexOf(bestLineEnd);
            List<int> pathIndex = new List<int>();

            path.Add(lines[indexStart]);
            pathIndex.Add(indexStart);

            for (int level = 0; level < timeLineSize; level++)
            {
                Console.WriteLine("\nLEVEL " + level);
                //Generate the prediction for the goal
                Array.Clear(ca1Network.modalities.First().reality, 0, ca1Network.modalities.First().reality.Length);
                ca1Network.modalities.First().reality[indexEnd, level] = 1.0;
                ca1Network.Converge();
                ca1Network.Diverge();
                double[,] goalPrediction = ca1Network.modalities.First().prediction.Clone() as double[,];
                printActivity(goalPrediction, "From goal");
                //Generate the prediction from the start
                Array.Clear(ca1Network.modalities.First().reality, 0, ca1Network.modalities.First().reality.Length);
                ca1Network.modalities.First().reality[indexStart, level] = 1.0;
                ca1Network.Converge();
                ca1Network.Diverge();
                double[,] startPrediction = ca1Network.modalities.First().prediction.Clone() as double[,];
                printActivity(startPrediction, "From start");

                double[,] mixedPrediction = new double[lines.Count, timeLineSize];
                CDZNET.Helpers.ArrayHelper.ForEach(mixedPrediction, true, (x, y) => { mixedPrediction[x, y] = goalPrediction[x, y] + startPrediction[x, y]; });
                printActivity(mixedPrediction, "Mixture");

                int bestLink = indexStart;
                int bestLevel = 0;
                double bestValue = 0;
                for (int i = 0; i < timeLineSize; i++)
                {
                    for (int j = 0; j < lines.Count; j++)
                    {
                        if (mixedPrediction[bestLink, bestLevel] > mixedPrediction[j, i])
                        {
                            bestLevel = i;
                            bestLink = j;
                            bestValue = mixedPrediction[j, i];
                            //Console.WriteLine("Best link found ");
                        }
                    }
                    Console.WriteLine();
                }
            }
            return true;
        }

        void printActivity(double[,] netAct, string label)
        {
            Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
            Console.WriteLine(label);
            for (int i = 0; i < netAct.GetLength(1); i++)
            {
                for (int j = 0; j < netAct.GetLength(0); j++)
                {
                    Console.Write(netAct[j, i].ToString("N2") + '\t');
                }
                Console.WriteLine();
            }
            Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
        }

        #endregion CA1 Autoassociator

        public TimeCanvas(int inputSize, int timeLineSize = 7, DistanceFunction distanceFunction = null)
        {
            this.inputSize = inputSize;
            this.timeLineSize = timeLineSize;
            if (distanceFunction == null)
                this.distanceFx = CDZNET.MathHelpers.distance;
            else
                this.distanceFx = distanceFunction;
        }

        public void Save(string filePath)
        {
            StreamWriter file = new StreamWriter(filePath);
            file.WriteLine(timeLineSize);

            //Write all the receptive fields on the first line    
            string lineData = "";
            foreach(TimeLine line in lines)
            {
                lineData += "(";
                for(int i=0;i<line.receptiveField.Count();i++)
                {
                    lineData += line.receptiveField[i];
                    if (i!=line.receptiveField.Count()-1)
                        lineData+=",";
                }
                lineData += ")";

                if (line != lines.Last())
                    lineData += ",";
            }
            file.WriteLine(lineData);

            //Then one line for each timeline
            foreach (TimeLine line in lines)
            {
                lineData = DEBUGConvert(line.receptiveField).ToString() ; 
                //lineData = ""; 
                foreach(TimeCell cell in line.cells)
                {
                    lineData += "(";
                    foreach(KeyValuePair< TimeCell, double > cellNext in cell.next)
                    {
                        lineData += "(";
                        int pointToIndex = lines.IndexOf(cellNext.Key.parentLine);
                        //lineData += pointToIndex + "," + cellNext.Value;
                        lineData += DEBUGConvert(cellNext.Key.parentLine.receptiveField).ToString() + "," + cellNext.Value;
                        lineData += ")";
                        if (cell.next.Last().Key != cellNext.Key)
                            lineData += ",";
                    }
                    lineData += ")";

                    if (line != lines.Last())
                        lineData += ",";
                }
                file.WriteLine(lineData);
            }
            file.Close();
        }

        public bool Load(string filepath)
        {
            StreamReader file = new StreamReader(filepath);
            bool isFine = true;

            lines = new List<TimeLine>();
            timeLineSize = Convert.ToInt16(file.ReadLine());
            string patternsLine = file.ReadLine();
            string[] patterns = patternsLine.Split(new string[] { "(","),(", ")" }, StringSplitOptions.RemoveEmptyEntries);
            int patternsSize = -1;
            foreach(string pattern in patterns)
            {
                string[] elements = pattern.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                if (patternsSize != -1 && patternsSize != elements.Count())
                    throw new Exception("Inconsistancy in the patterns size.");
                patternsSize = elements.Count();

                double[] dbPattern = new double[patternsSize];
                for (int i = 0; i < patternsSize; i++)
			    {
                    dbPattern[i] = Convert.ToDouble(elements[i]);
			    }
                lines.Add(new TimeLine(dbPattern, timeLineSize));       
            }

            //Then one line for each timeline
            int lineNumber = 0;
            foreach (TimeLine line in lines)
            {
                string lineData = "";
                try
                {
                    lineData = file.ReadLine();
                    string[] weights = lineData.Split(new string[] { "()", "((", ")),((", "))" }, StringSplitOptions.RemoveEmptyEntries);

                    for (int i = 0; i < line.cells.Count; i++)
                    {
                        string[] splitWeights = weights[i].Split(new string[] { "),(" }, StringSplitOptions.RemoveEmptyEntries);
                        if (splitWeights[0] == ",")
                            continue;

                        foreach (string weight in splitWeights)
                        {
                            string[] finallySplitWeight = weight.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                            int targetIndex = Convert.ToInt16(finallySplitWeight[0]);
                            double targetWeight = Convert.ToDouble(finallySplitWeight[1]);

                            TimeCell originCell = line.cells[i];
                            TimeCell targetCell = lines[targetIndex].cells[0];
                            originCell.next.Add(targetCell, targetWeight);
                            try
                            {
                                targetCell.previous[originCell] += 1;
                            }
                            catch (KeyNotFoundException)
                            {
                                targetCell.previous.Add(originCell, 1);
                            }
                        }
                    }
                    lineNumber++;
                    Console.WriteLine("Line=" + lineNumber + "/" + lines.Count);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Something was wrong at " + lineNumber + "\n\t Line data is : " + lineData);
                }
            }
            file.Close();
            return isFine;
        }

        public void Reset()
        {
            foreach(TimeLine l in lines)
            {
                l.Reset();
            }
        }

        public void Imprint(List<double[]> sequence, bool bidirectional = false)
        {
            //First convert all the element to their respective timeline
            List<TimeLine> tlSequence = new List<TimeLine>();
            foreach (double[] item in sequence)
            {
                TimeLine bestLine;
                double bestScore = 0.0;
                bool hasGoodTimeline = FindBestLine(item, out bestLine, out bestScore);

                //2-Create a new timeline if necessary
                if (!hasGoodTimeline)
                {
                    bestLine = new TimeLine(item, timeLineSize);
                    lines.Add(bestLine);
                }
                tlSequence.Add(bestLine);
            }

            //Second compute the relation among elements
            for (int oldestIndex = 0; oldestIndex < tlSequence.Count; oldestIndex++)
            {
                int elementsBelow = Math.Min(timeLineSize, tlSequence.Count - oldestIndex);
                for (int level = 1; level < elementsBelow; level++)
                {
                    try
                    {
                        tlSequence[oldestIndex].cells[level].next[tlSequence[oldestIndex + level].cells[0]] += 1;
                    }
                    catch (KeyNotFoundException)
                    {
                        tlSequence[oldestIndex].cells[level].next.Add(tlSequence[oldestIndex + level].cells[0], 1.0);
                    }

                    try
                    {
                        tlSequence[oldestIndex + level].cells[0].previous[tlSequence[oldestIndex].cells[level]] += 1;
                    }
                    catch (KeyNotFoundException)
                    {
                        tlSequence[oldestIndex + level].cells[0].previous.Add(tlSequence[oldestIndex].cells[level], 1.0);
                    }
                }
            }
        }

        public void Train(List<double[]> inputs, ref List<double[]> predictions, ref List<double> predictionsError, ref double meanPredictionsError)
        {
            Reset();
            meanPredictionsError = 0.0;
            for (int i = 0; i < inputs.Count; i++)
            {
                if (predictions != null)
                {
                    double[] prediction = Predict();
                    double[] reality = inputs[i];
                    double itemError = distanceFx(prediction, reality);
                    predictions.Add(prediction);
                    predictionsError.Add(itemError);
                    meanPredictionsError += itemError;
                }
                Train(inputs[i]);
            }
            meanPredictionsError /= inputs.Count;

            //The callback is done in Train(inputs[i])
            //if (onSequenceTraining != null)
            //    onSequenceTraining(inputs);
        }

        /// <summary>
        /// Teach the canvas with a new element added to the previous state.
        /// </summary>
        /// <param name="input"></param>
        public void Train(double[] input)
        {
            //1 - Find the best timeline and activate its first cell
            TimeLine bestLine = PresentInput(input, true, true);

            //4-Gather all the active cells sorted by how deep they are on the timeline
            SortedList<TimeCell, int> activeCells = new SortedList<TimeCell, int>();
            foreach(TimeLine line in lines)
            {
                foreach(TimeCell cell in line.cells)
                {
                    if (cell.isActive)
                        activeCells.Add(cell, cell.level);
                }
            }

            //5-Create/Strenghten connection between those cells & the bottom cell (the first one in the list)
            for (int i = 1; i < activeCells.Count;i++ )
            {
                //Next
                TimeCell cCell = activeCells.ElementAt(i).Key;

                if (!cCell.next.ContainsKey(bestLine.cells[0]))
                {
                    cCell.next.Add(bestLine.cells[0], 1);
                }
                else
                {
                    cCell.next[bestLine.cells[0]]++;
                }
                cCell.totalEncounters++;

                //Previous
                if (!bestLine.cells[0].previous.ContainsKey(cCell))
                {
                    bestLine.cells[0].previous.Add(cCell, 1);
                }
                else
                {
                    bestLine.cells[0].previous[cCell]++;
                }
            }

            //5.5 Callback
            if (onElementTraining != null)
                onElementTraining(input);

            //6-Make the activity propagate one step
            PropagateActivity();
        }

        public TimeLine PresentInput(double[] input, bool allowCreation, bool allowLearning)
        {
            //1-Find the winner timeline
            TimeLine bestLine = null;
            double bestDistance = double.PositiveInfinity;
            bool hasGoodTimeline = FindBestLine(input, out bestLine, out bestDistance);

            //2-Create a new timeline if necessary
            if (!hasGoodTimeline && allowCreation)
            {
                bestLine = new TimeLine(input, timeLineSize);
                lines.Add(bestLine);
            }
            //2-OR tune up the RF of the existing timeline
            else if (allowLearning)
            {
                for (int i = 0; i < inputSize; i++)
                {
                    double e = input[i] - bestLine.receptiveField[i];
                    bestLine.receptiveField[i] += parameterFFLearningRate * e;
                }
            }

            //3-Activate the first cell of the winner line
            bestLine.cells[0].isActive = true;

            //4-Callback
            if (onElementPresented != null)
                onElementPresented(input);

            return bestLine;
        }

        public void PropagateActivity()
        {
            foreach (TimeLine line in lines)
            {
                for (int level = line.cells.Count - 2; level >= 0; level--)
                {
                    line.cells[level + 1].isActive = line.cells[level].isActive;
                }
                line.cells[0].isActive = false;
            }
        }

        /// <summary>
        /// Scale down the weights so that the input/output "degrees" of every node is 1 (apply a softmax)
        /// </summary>
        public void ScaleWeights()
        {
            List<TimeCell> activeCells = new List<TimeCell>();
            foreach (TimeLine line in lines)
            {
                foreach (TimeCell cell in line.cells)
                {
                    //Previous
                    double sumPrevious = 0.0;
                    List<TimeCell> previousCells = new List<TimeCell>(cell.previous.Keys);
                    foreach (TimeCell p in previousCells)
                    {
                        sumPrevious += Math.Exp( cell.previous[p] );
                    }
                    foreach (TimeCell p in previousCells)
                    {
                        cell.previous[p] = Math.Exp(cell.previous[p]) / sumPrevious;
                    }

                    //Next
                    double sumNext = 0.0;
                    List<TimeCell> nextCells = new List<TimeCell>(cell.next.Keys);
                    foreach (TimeCell n in nextCells)
                    {
                        sumNext += Math.Exp(cell.next[n]);
                    }
                    foreach (TimeCell n in nextCells)
                    {
                        cell.next[n] = Math.Exp(cell.next[n]) / sumNext;
                    }
                }
            }
        }

        /// <summary>
        /// Get the list of active cell on the network or on a specific level
        /// </summary>
        /// <param name="level">the level to target (-1 for the entire network)</param>
        /// <returns></returns>
        public List<TimeCell> getActiveCells(int level = -1)
        {
            List<TimeCell> activeCells = new List<TimeCell>();
            foreach (TimeLine line in lines)
            {
                foreach(TimeCell cell in line.cells)
                {
                    if (cell.isActive && (level==-1||cell.level==level))
                        activeCells.Add(cell);
                }
            }
            return activeCells;
        }

        /// <summary>
        /// Find the best matching timeline for a given input.
        /// </summary>
        /// <param name="input">The input to match</param>
        /// <param name="bestLine">OUT the best timeline found.</param>
        /// <param name="bestDistance">OUT the best distance found.</param>
        /// <returns>True is the template is valid, false if a new one should be created.</returns>
        public bool FindBestLine(double[] input, out TimeLine bestLine, out double bestDistance)
        {
            bestLine = null;
            bestDistance = double.PositiveInfinity;
            foreach (TimeLine line in lines)
            {
                double cDistance = distanceFx(input, line.receptiveField);
                if (cDistance < bestDistance)
                {
                    bestDistance = cDistance;
                    bestLine = line;
                }
            }
            return !(bestLine == null || bestDistance > parameterLineCreationTreshold);
        }

        public double[] Predict(List< double[] > inputs, bool shouldCreateMissingPattern = true)
        {
            double[] output = new double[inputSize];
            Reset();

            if (inputs.Count>timeLineSize)
            {
                Console.WriteLine("WARNING: Predict() asked prediction based on more elements than the timeline length. Only the "+timeLineSize+" elements will be used.");
            }

            //1-Present the stimulus
            //The temporal order is index=0=oldest
            int startingIndex = Math.Max(0, inputs.Count-timeLineSize-1);
            for (int i = 0; i<timeLineSize; i++ )
            {
                //1-1 Find the best matching timeline
                TimeLine bestLine = null;
                double bestDistance = double.PositiveInfinity;
                bool hasGoodTimeline = FindBestLine(inputs[startingIndex+i], out bestLine, out bestDistance);
                if (!hasGoodTimeline && shouldCreateMissingPattern)
                {
                    bestLine = new TimeLine(inputs[startingIndex+i], timeLineSize);
                    lines.Add(bestLine);
                }

                //1-2 Set the cell corresponding to the right time level
                bestLine.cells[timeLineSize - i - 1].isActive = true;
            }

            //2-Generate the prediction
            return Predict();
        }

        /// <summary>
        /// Generate a prediction given the current state of the network
        /// </summary>
        /// <returns></returns>
        public double[] Predict()
        {
            TimeLine bestTimeLine = null;
            Dictionary<TimeLine, double> predictions = new Dictionary<TimeLine, double>();
            foreach(TimeLine line in lines)
            {
                predictions[line] = 1.0;
                TimeCell presentCell = line.cells[0];
                foreach (KeyValuePair<TimeCell,double> cell in presentCell.previous)
                {
                    if (cell.Key.isActive)
                    {
                        predictions[line] *= (1+cell.Value);
                    }
                }
                if (bestTimeLine == null || predictions[bestTimeLine]<predictions[line])
                {
                    bestTimeLine = line;
                }
            }

            if (bestTimeLine != null)
                return bestTimeLine.receptiveField;
            else
                return new double[inputSize];
        }

        /// <summary>
        /// Generate a prediction given the current state of the network
        /// </summary>
        /// <returns> A list of prediction and their score</returns>
        public List<KeyValuePair<double[], double>> PredictAll()
        {
            List<KeyValuePair<double[], double>> predictions = new List<KeyValuePair<double[], double>>();
            foreach (TimeLine line in lines)
            {
                double score = 0.0;
                TimeCell presentCell = line.cells[0];
                foreach (KeyValuePair<TimeCell, double> cell in presentCell.previous)
                {
                    if (cell.Key.isActive)
                    {
                        score += cell.Value;
                    }
                    //else
                    //{
                    //    score -= cell.Value;
                    //}
                }
                predictions.Add( new KeyValuePair<double[], double>(line.receptiveField, score) );
            }
            predictions.Sort((a, b) => a.Value.CompareTo( b.Value) );
            predictions.Reverse();
            return predictions;
        }

        /// <summary>
        /// Generate a prediction given the current state of the network
        /// Restrict the possible solution to the elements following directly the current state (first row on the timelines)
        /// </summary>
        /// <returns> A list of prediction and their score</returns>
        public List<KeyValuePair<double[], double>> PredictAllStrict()
        {
            //Get all the active cell of the first row
            List<TimeCell> currentState = getActiveCells(1);
            List<TimeCell> pastCells = getActiveCells();

            List<KeyValuePair<double[], double>> predictions = new List<KeyValuePair<double[], double>>();
            foreach (TimeLine line in lines)
            {                
                //It is not part of the history
                bool isPartOfHistory = false;
                foreach (TimeCell c in pastCells)
                {
                    if (line == c.parentLine)
                    {
                        isPartOfHistory = true;
                        break;
                    }
                }
                if (isPartOfHistory)
                    continue;

                //Check that this timeline is accessible from the active cells
                bool isAccessible = false;
                foreach (TimeCell c in currentState)
                {
                    foreach(TimeCell cNext in c.next.Keys)
                        if (cNext.parentLine == line)
                        {
                            isAccessible = true;
                            break;
                        }
                    if (isAccessible)
                        break;
                }
                if (!isAccessible)
                    continue;

                double score = 0.0;
                TimeCell presentCell = line.cells[0];
                foreach (KeyValuePair<TimeCell, double> cell in presentCell.previous)
                {
                    //char predictedTimeline = DEBUGConvert(line.receptiveField);
                    //char predictingTimeline = DEBUGConvert(cell.Key.parentLine.receptiveField);
                    if (cell.Key.isActive)
                    {
                        score += cell.Value;
                    }
                    else
                    {
                        score -= cell.Value;
                    }
                }
                predictions.Add(new KeyValuePair<double[], double>(line.receptiveField, score));
            }
            predictions.Sort((a, b) => a.Value.CompareTo(b.Value));
            predictions.Reverse();
            return predictions;
        }

        #region DEBUG
        public Dictionary<double[], char> c2;
        char DEBUGConvert(double[] d)
        {
            //find the closest
            foreach (double[] real in c2.Keys)
            {
                if (real.SequenceEqual(d))
                    return c2[real];
            }

            return '#';
        }
        #endregion DEBUG

        /// <summary>
        /// Find the shortest (?) path between 2 elements
        /// </summary>
        /// <param name="start">Starting point</param>
        /// <param name="end">Goal to reach</param>
        /// <param name="path">Oredered path of TimeLines to follow (take the receptive field of each for points)</param>
        /// <returns>true if a path was found, false if not</returns>
        public bool findPath(double[] start, double[] end, out List<TimeLine> path)
        {
            path = new List<TimeLine>();

            TimeLine bestLineStart = null;
            double bestDistanceStart = double.PositiveInfinity;
            bool hasGoodTimelineStart = FindBestLine(start, out bestLineStart, out bestDistanceStart);

            TimeLine bestLineEnd = null;
            double bestDistanceEnd = double.PositiveInfinity;
            bool hasGoodTimelineEnd = FindBestLine(end, out bestLineEnd, out bestDistanceEnd);

            //If we do not have good representation for the start or the end we give up.
            //We could also take the closest ones...
            if ( !(hasGoodTimelineStart&&hasGoodTimelineEnd) )
                return false;

            //Build the list of all the lines that can lead to this goal
            //We could sort it by their scores
            List<TimeLine> leadingLines = new List<TimeLine>();
            Dictionary<TimeLine, TimeCell> leadingCells = new Dictionary<TimeLine,TimeCell>();

            foreach(KeyValuePair<TimeCell,double> leadingCell in bestLineEnd.cells[0].previous)
            {
                leadingLines.Add(leadingCell.Key.parentLine);
                leadingCells[leadingCell.Key.parentLine] = leadingCell.Key;
            }

            List<TimeLine> initialLeadingLines = new List<TimeLine>(leadingLines);

            //Main idea: Keep the predecessors activated & propagate the current state back in time
            TimeLine currentTimeLine = bestLineStart;
            path.Add(currentTimeLine);
            bool isPathComplete = false;
            while(!isPathComplete)
            {
                TimeCell bestPredecessor = null;
                int bestStartingLevel = timeLineSize;
                char DEBUGCurrentLine = DEBUGConvert(currentTimeLine.receptiveField);

                // propagate the current state back in time
                for (int currentIndexOnLine = 0; currentIndexOnLine < timeLineSize; currentIndexOnLine++)
                {
                    //Find the next cell that will lead to the shortest path to the goal
                    foreach (KeyValuePair<TimeCell, double> nextProbas in currentTimeLine.cells[currentIndexOnLine].next)
                    {
                        char DEBUGConnectedLine = DEBUGConvert(nextProbas.Key.parentLine.receptiveField);
                        //If the current state leads to a goal-leading line && this leading line is the shortest so far
                        if (leadingLines.Contains(nextProbas.Key.parentLine))
                        {
                            TimeCell leadingCell = leadingCells[nextProbas.Key.parentLine];
                            if (bestPredecessor == null || bestPredecessor.level + bestStartingLevel > leadingCell.level + currentIndexOnLine)
                            {
                                bestPredecessor = leadingCell;
                                bestStartingLevel = currentIndexOnLine;
                            }
                        }
                    }
                }

                if (bestPredecessor != null)
                {
                    currentTimeLine = bestPredecessor.parentLine;
                    path.Add(currentTimeLine);

                    //THIS IS SUPER NOT OPTIMAL
                    if (initialLeadingLines.Count != leadingLines.Count)
                    {
                        leadingLines = new List<TimeLine>(initialLeadingLines);
                    }

                    //We detect the end of the path by checking that the predecessor is just 1 element behind in time
                    else if (bestPredecessor.level == 1)
                    {
                        path.Add(bestLineEnd);
                        isPathComplete = true;
                    }
                }
                else
                {
                    //Workaround the 1 element problem
                    if (leadingLines.Contains(currentTimeLine))
                    {
                        path.Add(bestLineEnd);
                        isPathComplete = true;
                        break;
                    }

                    //Problem: there is no 2 overlapping paths
                    //We expend the leading line of one level (take the leading lines leading to those leading lines)
                    List<TimeLine> completedLines = new List<TimeLine>(leadingLines);
                    foreach(TimeLine L in leadingLines)
                    {
                        foreach (KeyValuePair<TimeCell, double> leadingCell in L.cells[0].previous)
                        {
                            if (!leadingLines.Contains(leadingCell.Key.parentLine))
                            {
                                completedLines.Add(leadingCell.Key.parentLine);
                                leadingCells[leadingCell.Key.parentLine] = leadingCell.Key;
                            }
                        }
                    }
                    if (leadingLines.Count == completedLines.Count)
                    {
                        //we could not add more predecessor. There is not path.
                        break;
                    }
                    else
                    {
                        leadingLines = completedLines;
                    }
                }
            }
            return isPathComplete;
        }
    }
}
