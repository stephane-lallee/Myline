﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeCells
{
    class Program
    {
        public static Dictionary<char, double[]> c;
        public static Dictionary<double[], char> c2;

        [Serializable]
        class Sequence : List<double[]>
        {
            public string ToReadable()
            {
                string str = "";
                for(int i=0; i<Count;i++)
                {
                    str += "Elt " + i + "-->\t";
                    for (int j = 0; j < this[i].Length; j++)
                    {
                        str += this[i][j] + "\t";
                    }
                    str += "\n";
                }
                return str;
            }
        }

        static void Main(string[] args)
        {

            formLetters(out c, out c2);
            //double[] a = { 1, 0, 0, 0, 0 };
            //double[] b = { 0, 1, 0, 0, 0 };
            //double[] c = { 0, 0, 1, 0, 0 };
            //...
            List<Sequence> sequenceTestground = new List<Sequence>() 
            { 
                new Sequence() { c['a'], c['b'] },         
                new Sequence() { c['b'], c['c'] },         
                new Sequence() { c['c'], c['d'] },           
            };

            List<Sequence> sequenceGoalDirected = new List<Sequence>() 
            { 
                new Sequence() { c['c'], c['g'], c['k'], c['j'], c['i'] },
                new Sequence() { c['c'], c['g'], c['k'], c['o'], c['s'] },
                new Sequence() { c['c'], c['g'], c['k'], c['l'], c['m'] }            
            };

            List<Sequence> sequenceShortcuts = new List<Sequence>() 
            { 
                new Sequence() { c['a'], c['b'], c['c'], c['g'], c['k'], c['o'], c['s'], c['t'], c['u']  },
                new Sequence() { c['i'], c['j'], c['k'], c['l'], c['m'], c['p'], c['u'], c['t'], c['s'], c['r'], c['q']  },
                new Sequence() { c['a'], c['f'], c['i'], c['n'], c['q'] },
                new Sequence() { c['m'], c['h'], c['e'], c['d'], c['c'], c['b'], c['a'], c['f'], c['i'] }            
            };

            //List<Sequence> setToUse = sequenceGoalDirected;
            List<Sequence> setToUse = sequenceShortcuts;

            Console.WriteLine("Training... CA3 (Sequences)");
            TimeCanvas canvas;
            bool bidirectionalTraining = true;
            trainImprint(out canvas, setToUse, 1, false);
            canvas.EnableCA1();
            canvas.EnableGoal();
            trainNormal(canvas, setToUse, 1, bidirectionalTraining);
            //canvas.TrainCA1();
            canvas.ScaleWeights();
            Console.WriteLine("Training over.");

            //foreach (Sequence s in setToUse)
            //{
            //    string errorMsg = "\n\n ---------------------------------------- \n Sequence \n";
            //    double seqMeanError = 0.0;

            //    canvas.Reset();
            //    foreach (double[] item in s)
            //    {
            //        if (item != s.First())
            //        {
            //            List<KeyValuePair<double[], double>> predictions = canvas.PredictAllStrict();
            //            double[] reality = item;
            //            double itemError = CDZNET.MathHelpers.distance(predictions.First().Key, reality);
            //            seqMeanError += itemError;
            //            errorMsg += "ActiveCells=" + canvas.getActiveCells().Count + "\n";
            //            errorMsg += "Reality \t" + c2[reality] + "\n";
            //            //errorMsg += "Predict \t"  + Convert(prediction, c2) + "\t";
            //            errorMsg += "Prediction TC \t";
            //            foreach (KeyValuePair<double[], double> pre in predictions)
            //            {
            //                errorMsg += Convert(pre.Key, c2) + "(" + pre.Value.ToString("N2") + ")" + " ";
            //            }
            //            errorMsg += "\nErrorTC   \t " + itemError;

            //            List<KeyValuePair<double[], double>> predictionsCA1 = canvas.PredictAllCA1();
            //            errorMsg += "\nPrediction CA1 \t";
            //            foreach (KeyValuePair<double[], double> pre in predictionsCA1)
            //            {
            //                errorMsg += Convert(pre.Key, c2) + "(" + pre.Value.ToString("N2") + ")" + " ";
            //            }
            //            double itemErrorCA1 = CDZNET.MathHelpers.distance(predictionsCA1.First().Key, reality);
            //            errorMsg += "\nErrorCA1   \t " + itemErrorCA1 + "\n ---- \n";
            //        }
            //        canvas.PresentInput(item, false, false);
            //        canvas.PropagateActivity();
            //    }
            //    Console.WriteLine(errorMsg);
            //}
            Console.WriteLine("Press a key to continue...");
            Console.ReadKey();

            Console.WriteLine("Training... CA1 (Goal Oriented)");

            canvas.TrainGoalNetwork();
            //canvas.ScaleWeights();
            Console.WriteLine("Training over.");

            //Test the pathfinding
            List<Sequence> pathToTest = new List<Sequence>
            {                
                //new Sequence{c['c'], c['i']},
                //new Sequence{c['c'], c['s']},
                //new Sequence{c['c'], c['m']}
                
                new Sequence{c['a'], c['u']},
                new Sequence{c['i'], c['q']},
                new Sequence{c['a'], c['q']},
                new Sequence{c['m'], c['i']},

                new Sequence{c['q'], c['o']},
                new Sequence{c['a'], c['k']},
                new Sequence{c['f'], c['r']}
            };

            Console.WriteLine("\n------------------\nFinding paths...");
            //while (true)
            {
                foreach (Sequence seq in pathToTest)
                {
                    Console.WriteLine("From " + Convert(seq.First(), c2) + " to " + Convert(seq.Last(), c2));
                    //List<TimeLine> autoPath;
                    //bool pathFoundAuto = canvas.findPathGoalNetwork(seq.First(), seq.Last(), out autoPath);
                    //Console.WriteLine("Autoassociator path = " + pathToString(autoPath));

                    List<TimeLine> heteroPath;
                    bool pathFoundHetero = canvas.findPathGoalNetworkIO(seq.First(), seq.Last(), out heteroPath);
                    //canvas.findPath(c['a'], c['d'], out path);
                    Console.WriteLine("Heteroassociator path = " + pathToString(heteroPath));

                    Console.WriteLine("Press a key to continue...");
                    Console.ReadKey();
                }
                Console.WriteLine("Press a key to exit...");
                Console.ReadKey();
            }
            Console.WriteLine("Finding paths, over.");
        }

        static string pathToString(List<TimeLine> path)
        {
            string pathStr = "Path = ";
            foreach (TimeLine pathElement in path)
            {
                pathStr += Convert(pathElement.receptiveField, c2);
                if (pathElement != path.Last())
                    pathStr += "->";
            }
            return pathStr;
        }

        static void trainImprint(out TimeCanvas canvas, List<Sequence> trainSet, int iterations, bool bidirectional)
        {
            canvas = new TimeCanvas(25, 7);
            canvas.c2 = c2;

            for (int i = 0; i < iterations; i++)
            {
                foreach (Sequence s in trainSet)
                {
                    canvas.Imprint(s, bidirectional);
                }
            }
            canvas.Save("debugImprint.csv");
        }
        static void trainNormal(TimeCanvas canvas, List<Sequence> trainSet, int iterations, bool bidirectional)
        {
            //canvas = new TimeCanvas(25, 7);
            //canvas.c2 = c2;

            for (int i = 0; i < iterations; i++)
            {
                foreach (Sequence s in trainSet)
                {
                    canvas.Reset();
                    foreach (double[] item in s)
                    {
                        canvas.Train(item);
                    }

                    if (bidirectional)
                    {
                        Sequence rS = ObjectCopier.Clone(s);
                        rS.Reverse();

                        canvas.Reset();
                        foreach (double[] item in rS)
                        {
                            canvas.Train(item);
                        }
                    }
                }
            }
            canvas.Save("debugNormal.csv");
        }

        static void formLetters(out Dictionary< char, double[] > char2code, out Dictionary< double[], char > code2char)
        {
            char2code = new Dictionary<char, double[]>();
            code2char = new Dictionary<double[], char>();

            for(char c = 'a'; c<'z';c++)
            {
                double[] code = new double[25];
                for(int i=0;i<25;i++)
                {
                    if (i == (c - 'a'))
                        code[i] = 1;
                    else
                        code[i] = 0;
                }
                char2code[c] = code;
                code2char[code] = c;
            }
        }
        static char Convert(double[] d, Dictionary<double[], char> code2char)
        {
            //find the closest
            foreach(double[] real in code2char.Keys)
            {
                if (real.SequenceEqual(d))
                    return code2char[real];
            }

            return '#';      
        }

        static string Convert(double[] d)
        {
            string str = "";
            for (int i = 0; i < d.Length; i++)
            {
                str += d[i] + "\t";   
            }
            return str;
        }
    }
}
