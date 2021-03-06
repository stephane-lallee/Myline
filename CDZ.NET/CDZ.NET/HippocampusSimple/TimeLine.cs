﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeCells
{
    public class TimeLine
    {
        public List<TimeCell> cells = new List<TimeCell>();
        public double[] receptiveField;

        public TimeLine(double[] receptiveField, int lineLength)
        {
            this.receptiveField = receptiveField.Clone() as double[];
            for(int i=0;i<lineLength;i++)
            {
                cells.Add(new TimeCell(i, this));
            }
        }

        public void Reset()
        {
            foreach(TimeCell cell in cells)
            {
                cell.isActive = false;
            }
        }

        public void Update(double[] input)
        {

        }
    }
}
