using System;
namespace LumenBar;
public readonly record struct CompactLayout(int Columns,int Rows,double Width,double Height)
{
    public static CompactLayout Fit(double width,double height,int count,bool verticalDrag=false)
    {
        count=Math.Clamp(count,1,5);width=Math.Max(100,width);height=Math.Max(48,height);
        int best=1;double score=double.MaxValue;
        for(int cols=1;cols<=count;cols++)
        {
            if(count%cols!=0)continue;
            double aspect=(width/cols)/(height/(count/cols));
            double error=Math.Abs(Math.Log(aspect/3.2));if(error<score){score=error;best=cols;}
        }
        int rows=count/best;double cellAspect=(width/best)/(height/rows);
        if(cellAspect<2.6 || cellAspect>4.0)
        {
            double target=Math.Clamp(cellAspect,2.6,4.0);
            if(verticalDrag)width=height/rows*best*target;else height=width/best*rows/target;
        }
        return new(best,rows,width,height);
    }
}
