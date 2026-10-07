using System;
namespace LumenBar;

// Screen coordinates avoid feedback when the window moves beneath the pointer.
public sealed class HoldDrag
{
    public const int HoldMilliseconds=200;
    public bool Pressed {get;private set;}
    public bool Moving {get;private set;}
    int pointerX,pointerY,left,top;double threshold;long pressedAt;
    public void Begin(int x,int y,int windowLeft,int windowTop,long now,double scale)
    {Pressed=true;Moving=false;pointerX=x;pointerY=y;left=windowLeft;top=windowTop;pressedAt=now;threshold=4*scale;}
    public (int X,int Y)? Move(int x,int y,bool leftButtonDown,long now)
    {
        if(!Pressed)return null;
        if(!leftButtonDown){End();return null;}
        if(!Moving && (now-pressedAt<HoldMilliseconds || Math.Max(Math.Abs(x-pointerX),Math.Abs(y-pointerY))<threshold))return null;
        Moving=true;return(left+x-pointerX,top+y-pointerY);
    }
    public bool End(){bool moved=Moving;Pressed=false;Moving=false;return moved;}
}
