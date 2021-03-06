﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.IO;

using CDZNET;
using CDZNET.Core;
using CDZNET.GUI;

namespace VowelWorldModel
{
    public partial class ModalPx : Form
    {
        Random rnd = new Random();
        //Parameters
        MMNodeFactory.Model modelUsed = MMNodeFactory.Model.MWSOM;
        int retinaSize = 3;
        int shapeCount = 4;
        int worldWidth = 100;
        int worldHeight = 100;
        int seedsNumber = 3;
        int trainSteps = 1000;
        double orientationVariability = 0.0; //degrees
        World world;
        Dictionary<string, Bitmap> worldVisu;

        //Network structures
        //1-Inputs
        CDZNET.Core.Signal[,] LEC_Color; //Visual matrix

        //2-Areas
        CDZNET.Core.MMNode CA3;

        //Thread
        //public Thread mainLoopThread; 

        public ModalPx()
        {
            InitializeComponent();

            //Generate the world
            world = new World(worldWidth, worldHeight, shapeCount, orientationVariability);
            world.Randomize(seedsNumber);
            getWorldVisualization();

            //Generate the network
            //1-Areas          
            CA3 = MMNodeFactory.obtain(modelUsed);

            //2-Inputs
            LEC_Color = new Signal[retinaSize, retinaSize];
            for (int i = 0; i < retinaSize; i++)
            {
                for (int j = 0; j < retinaSize; j++)
                {
                    LEC_Color[i, j] = new CDZNET.Core.Signal(4, 1);
                    CA3.addModality(LEC_Color[i, j], "Color_"+i+"_"+j);
                }                
            }

            //Gui purpose
            ctrlCA3.attach(CA3);
        }

        public void learnAndLog()
        {
            //Explore the world


            int steps = 0;
            int worldCnt = 0;
            progressBarCurrentOp.Minimum = 0;
            progressBarCurrentOp.Maximum = 3 * trainSteps;
            progressBarCurrentOp.Step = 1;
            while (steps<trainSteps)
            {
                progressBarCurrentOp.PerformStep();
                step();
                steps++;
            }
            MessageBox.Show("Start test.");
            TestMissingPoint();
            TestMissingLineBiColor();
            TestFullBiColor();
            MessageBox.Show("Done.");
        }

        void step()
        {
            //-----------------
            //Fixate somewhere
            int startX = rnd.Next(0, world.Width - retinaSize);
            int startY = rnd.Next(0, world.Height - retinaSize);

            //-----------------
            //Get the perceptive information
            //2-Vision
            for (int i = 0; i < retinaSize; i++)
            {
                for (int j = 0; j < retinaSize; j++)
                {
                    Cell startPx = world.cells[startX + i, startY + j];
                    Signal signal = LEC_Color[i, j];
                    for (int compo = 0; compo < 4; compo++)
                    {
                        signal.reality[compo, 0] = startPx.colorCode[compo];
                    }
                }
            }

            //-----------------
            //Run a cycle on CA3
            CA3.Converge();
            CA3.Diverge();
        }
        void TestMissingPoint()
        {
            CA3.learningLocked = true;

            double[,] codes = {   {1, 0, 0, 0} ,
                                  {0, 1, 0, 0} ,
                                  {0, 0, 1, 0} ,
                                  {0, 0, 0, 1} ,
                              };

            StreamWriter logFile = new StreamWriter("missingPoint.csv");
            logFile.Write("missingLine,");
            logFile.Write("color,");
            for (int i = 0; i < retinaSize; i++)
            {
                for (int j = 0; j < retinaSize; j++)
                {
                    for (int compo = 0; compo < 4; compo++)
                    {
                        logFile.Write("reality" + i + "_" + j + "_" + compo + ",");
                        logFile.Write("prediction" + i + "_" + j + "_" + compo + ",");
                    }
                }
            }
            logFile.WriteLine();

            for (int missingLine = 0; missingLine < retinaSize; missingLine++)
            {
                //Create the pattern to complete
                for (int colorUsedA = 0; colorUsedA < 4; colorUsedA++)
                {
                    for (int colorUsedB = 0; colorUsedB < 4; colorUsedB++)
                    {
                        for (int i = 0; i < retinaSize; i++)
                        {
                            for (int j = 0; j < retinaSize; j++)
                            {
                                //Before the line
                                if (missingLine <= i)
                                {
                                    CA3.modalitiesInfluence[LEC_Color[i, j]] = 1.0;
                                    //Assign the color
                                    for (int compo = 0; compo < 4; compo++)
                                    {
                                        LEC_Color[i, j].reality[compo, 0] = codes[colorUsedA, compo];
                                    }
                                }
                                //After the line
                                if (missingLine > i)
                                {
                                    CA3.modalitiesInfluence[LEC_Color[i, j]] = 1.0;
                                    //Assign the color
                                    for (int compo = 0; compo < 4; compo++)
                                    {
                                        LEC_Color[i, j].reality[compo, 0] = codes[colorUsedB, compo];
                                    }
                                }
                                //Missing line
                                if (missingLine == i && missingLine == j)
                                {
                                    CA3.modalitiesInfluence[LEC_Color[i, j]] = 0.0;

                                    ////Assign the color
                                    //for (int compo = 0; compo < 4; compo++)
                                    //{
                                    //    LEC_Color[i, j].reality[compo, 0] = codes[colorUsedA, compo];
                                    //}
                                }
                            }
                        }

                        //Cycle & predict
                        CA3.Converge();
                        CA3.Diverge();

                        //Dump
                        logFile.Write(missingLine + ",");
                        logFile.Write(colorUsedA + "->" + colorUsedB + ",");
                        Bitmap bmp = new Bitmap(retinaSize * 2, retinaSize);
                        for (int i = 0; i < retinaSize; i++)
                        {
                            for (int j = 0; j < retinaSize; j++)
                            {
                                double[] compoVectorReal = new double[4];
                                double[] compoVectorPred = new double[4];
                                for (int compo = 0; compo < 4; compo++)
                                {
                                    if (missingLine == i && missingLine == j)
                                    {
                                        logFile.Write("?,");
                                    }
                                    else
                                    {
                                        logFile.Write(LEC_Color[i, j].reality[compo, 0] + ",");
                                        compoVectorReal[compo] = LEC_Color[i, j].reality[compo, 0];
                                    }

                                    logFile.Write(LEC_Color[i, j].prediction[compo, 0] + ",");
                                    compoVectorPred[compo] = LEC_Color[i, j].prediction[compo, 0];
                                }

                                if (missingLine == i && missingLine == j)
                                    bmp.SetPixel(i, j, Color.Black);
                                else
                                    bmp.SetPixel(i, j, Cell.getColorFromCode(compoVectorReal));
                                bmp.SetPixel(retinaSize + i, j, Cell.getColorFromCode(compoVectorPred));

                                //separation
                                //bmp.SetPixel(retinaSize + 1, j, Color.White);
                            }
                        }
                        //bmp.Save("test" + missingLine + "_" + colorUsedA + "_" + colorUsedB + "_" + ".bmp");
                        Bitmap bmp2 = new Bitmap(bmp, new Size(50 * 2, 50));
                        bmp.Save("testLine" + missingLine + "_" + colorUsedA + "_" + colorUsedB + "_" + ".bmp");

                        logFile.WriteLine();
                    }
                }
            }
            logFile.Close();
        }
        void TestFullBiColor()
        {
            CA3.learningLocked = true;

            double[,] codes = {   {1, 0, 0, 0} ,
                                  {0, 1, 0, 0} ,
                                  {0, 0, 1, 0} ,
                                  {0, 0, 0, 1} ,
                              };

            StreamWriter logFile = new StreamWriter("fullBicolor.csv");
            logFile.Write("missingLine,");
            logFile.Write("color,");
            for (int i = 0; i < retinaSize; i++)
            {
                for (int j = 0; j < retinaSize; j++)
                {
                    for (int compo = 0; compo < 4; compo++)
                    {
                        logFile.Write("reality" + i + "_" + j + "_" + compo + ",");
                        logFile.Write("prediction" + i + "_" + j + "_" + compo + ",");
                    }
                }
            }
            logFile.WriteLine();

            //Set the influence
            for (int missingLine = 0; missingLine < retinaSize; missingLine++)
            {
                //Create the pattern to complete
                for (int colorUsedA = 0; colorUsedA < 4; colorUsedA++)
                {
                    for (int colorUsedB = 0; colorUsedB < 4; colorUsedB++)
                    {
                        for (int i = 0; i < retinaSize; i++)
                        {
                            for (int j = 0; j < retinaSize; j++)
                            {
                                //Before the line
                                if (missingLine < i)
                                {
                                    CA3.modalitiesInfluence[LEC_Color[i, j]] = 1.0;
                                    //Assign the color
                                    for (int compo = 0; compo < 4; compo++)
                                    {
                                        LEC_Color[i, j].reality[compo, 0] = codes[colorUsedA, compo];
                                    }
                                }
                                //After the line
                                if (missingLine >= i)
                                {
                                    CA3.modalitiesInfluence[LEC_Color[i, j]] = 1.0;
                                    //Assign the color
                                    for (int compo = 0; compo < 4; compo++)
                                    {
                                        LEC_Color[i, j].reality[compo, 0] = codes[colorUsedB, compo];
                                    }
                                }
                                ////Missing line
                                //if (missingLine == i)
                                //{
                                //    CA3.modalitiesInfluence[LEC_Color[i, j]] = 0.0;

                                //    //Assign the color
                                //    for (int compo = 0; compo < 4; compo++)
                                //    {
                                //        LEC_Color[i, j].reality[compo, 0] = codes[colorUsedA, compo];
                                //    }
                                //}
                            }
                        }

                        //Cycle & predict
                        CA3.Converge();
                        CA3.Diverge();

                        //Dump
                        logFile.Write(missingLine + ",");
                        logFile.Write(colorUsedA + "->" + colorUsedB + ",");
                        Bitmap bmp = new Bitmap(retinaSize * 2, retinaSize);
                        for (int i = 0; i < retinaSize; i++)
                        {
                            for (int j = 0; j < retinaSize; j++)
                            {
                                double[] compoVectorReal = new double[4];
                                double[] compoVectorPred = new double[4];
                                for (int compo = 0; compo < 4; compo++)
                                {
                                    //if (missingLine == i)
                                    //{
                                    //    logFile.Write("?,");
                                    //}
                                    //else
                                    {
                                        logFile.Write(LEC_Color[i, j].reality[compo, 0] + ",");
                                        compoVectorReal[compo] = LEC_Color[i, j].reality[compo, 0];
                                    }

                                    logFile.Write(LEC_Color[i, j].prediction[compo, 0] + ",");
                                    compoVectorPred[compo] = LEC_Color[i, j].prediction[compo, 0];
                                }

                                //if (missingLine == i)
                                //    bmp.SetPixel(i, j, Color.Black);
                                //else
                                    bmp.SetPixel(i, j, Cell.getColorFromCode(compoVectorReal));
                                bmp.SetPixel(retinaSize + i, j, Cell.getColorFromCode(compoVectorPred));

                                //separation
                                //bmp.SetPixel(retinaSize + 1, j, Color.White);
                            }
                        }
                        //bmp.Save("test" + missingLine + "_" + colorUsedA + "_" + colorUsedB + "_" + ".bmp");
                        Bitmap bmp2 = new Bitmap(bmp, new Size(50 * 2, 50));
                        bmp.Save("testFull" + missingLine + "_" + colorUsedA + "_" + colorUsedB + "_" + ".bmp");

                        logFile.WriteLine();
                    }
                }
            }
            logFile.Close();
        }
        void TestMissingLineBiColor()
        {
            CA3.learningLocked = true;

            double[,] codes = {   {1, 0, 0, 0} ,
                                  {0, 1, 0, 0} ,
                                  {0, 0, 1, 0} ,
                                  {0, 0, 0, 1} ,
                              };

            StreamWriter logFile = new StreamWriter("missingLinesBicolor.csv");
            logFile.Write("missingLine,");
            logFile.Write("color,");
            for (int i = 0; i < retinaSize; i++)
            {
                for (int j = 0; j < retinaSize; j++)
                {
                    for (int compo = 0; compo < 4; compo++)
                    {
                        logFile.Write("reality" + i + "_" + j + "_" + compo + ",");
                        logFile.Write("prediction" + i + "_" + j + "_" + compo + ",");
                    }
                }
            }
            logFile.WriteLine();

            //Set the influence
            for (int missingLine = 0; missingLine < retinaSize; missingLine++)
            {
                //Create the pattern to complete
                for (int colorUsedA = 0; colorUsedA < 4; colorUsedA++)
                {
                    for (int colorUsedB = 0; colorUsedB < 4; colorUsedB++)
                    {
                        for (int i = 0; i < retinaSize; i++)
                        {
                            for (int j = 0; j < retinaSize; j++)
                            {
                                //Before the line
                                if (missingLine < i)
                                {
                                    CA3.modalitiesInfluence[LEC_Color[i, j]] = 1.0;
                                    //Assign the color
                                    for (int compo = 0; compo < 4; compo++)
                                    {
                                        LEC_Color[i, j].reality[compo, 0] = codes[colorUsedA, compo];
                                    }
                                }
                                //After the line
                                if (missingLine > i)
                                {
                                    CA3.modalitiesInfluence[LEC_Color[i, j]] = 1.0;
                                    //Assign the color
                                    for (int compo = 0; compo < 4; compo++)
                                    {
                                        LEC_Color[i, j].reality[compo, 0] = codes[colorUsedB, compo];
                                    }
                                }
                                //Missing line
                                if (missingLine == i)
                                {
                                    CA3.modalitiesInfluence[LEC_Color[i, j]] = 0.0;

                                    //Assign the color
                                    for (int compo = 0; compo < 4; compo++)
                                    {
                                        LEC_Color[i, j].reality[compo, 0] = codes[colorUsedA, compo];
                                    }
                                }
                            }
                        }

                        //Cycle & predict
                        CA3.Converge();
                        CA3.Diverge();

                        //Dump
                        logFile.Write(missingLine + ",");
                        logFile.Write(colorUsedA +"->"+ colorUsedB + ",");
                        Bitmap bmp = new Bitmap(retinaSize*2, retinaSize);
                        for (int i = 0; i < retinaSize; i++)
                        {
                            for (int j = 0; j < retinaSize; j++)
                            {
                                double[] compoVectorReal = new double[4];
                                double[] compoVectorPred = new double[4];
                                for (int compo = 0; compo < 4; compo++)
                                {
                                    if (missingLine == i)
                                    {
                                        logFile.Write("?,");
                                    }
                                    else
                                    {
                                        logFile.Write(LEC_Color[i, j].reality[compo, 0] + ",");
                                        compoVectorReal[compo] = LEC_Color[i, j].reality[compo, 0];
                                    }

                                    logFile.Write(LEC_Color[i, j].prediction[compo, 0] + ",");
                                    compoVectorPred[compo] = LEC_Color[i, j].prediction[compo, 0];
                                }
                                
                                if (missingLine == i)
                                    bmp.SetPixel(i, j, Color.Black);
                                else
                                    bmp.SetPixel(i, j, Cell.getColorFromCode(compoVectorReal));
                                bmp.SetPixel(retinaSize + i, j, Cell.getColorFromCode(compoVectorPred));
                                
                                //separation
                                //bmp.SetPixel(retinaSize + 1, j, Color.White);
                            }
                        }
                        //bmp.Save("test" + missingLine + "_" + colorUsedA + "_" + colorUsedB + "_" + ".bmp");
                        Bitmap bmp2 = new Bitmap(bmp, new Size(50*2,50));
                        bmp.Save("testPoint" + missingLine + "_" + colorUsedA + "_" + colorUsedB + "_" + ".bmp");

                        logFile.WriteLine();
                    }
                }
            }
            logFile.Close();
        }

        //---------------------VISUALIZATION------------------
        #region Visualization

        Bitmap getBitmapFromColorCode(double[,] a)
        {
            Bitmap bmp = new Bitmap(a.GetLength(0) / 4, a.GetLength(1));
            for (int i = 0; i < a.GetLength(0); i+=4)
            {
                for (int j = 0; j < a.GetLength(1); j++)
                {
                    bmp.SetPixel(i/4, j, Cell.getColorFromCode(new double[] { a[i, j], a[i + 1, j], a[i + 2, j], a[i + 3, j] }));
                }
            }
            return bmp;
        }

        void getWorldVisualization()
        {
            worldVisu = world.toImages();
            pictureBoxWorldColor.Image = worldVisu["color"].Clone() as Bitmap;
            pictureBoxWorldColor.Refresh();;
        }

        void drawFixationPoint(int x, int y)
        {
            if (pictureBoxWorldColor.InvokeRequired)
                this.Invoke(new Action<int, int>(drawFixationPoint), x, y);
            else
            {
                pictureBoxWorldColor.Image = worldVisu["color"].Clone() as Bitmap;
                using (Graphics g = Graphics.FromImage(pictureBoxWorldColor.Image))
                    g.DrawRectangle(new Pen(Color.Black, 2), x - retinaSize / 2, y - retinaSize / 2, retinaSize, retinaSize);
                pictureBoxWorldColor.Refresh();
            }
        }
        #endregion

        //---------------------GUI------------------
        private void buttonLearnAndLog_Click(object sender, EventArgs e)
        {
            learnAndLog();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            step();
        }

        private void buttonRdmz_Click(object sender, EventArgs e)
        {
            world.Randomize(seedsNumber);
            getWorldVisualization();
        }
    }
}
