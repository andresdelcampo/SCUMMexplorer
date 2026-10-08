// Diagnostic host for the unmodified ScummVM 2.2.0 v4 walking method bodies.
// Generated methods are local/ignored. This host models kinematics only:
// animation, facing, rendering, script dispatch and sound are deliberately inert.
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <iostream>
#include <vector>
using byte = uint8_t;
using uint16 = uint16_t;
using int32 = int32_t;
using uint = unsigned int;
#define ABS(x) std::abs(x)
#define SWAP(a,b) std::swap(a,b)
#define V12_X_MULTIPLIER 1
#define V12_Y_MULTIPLIER 1
enum { GID_DIG, GID_CMI, MF_NEW_LEG=1, MF_IN_LEG=2, MF_TURN=4, MF_LAST_LEG=8, MF_FROZEN=16 };
namespace Common {
struct Point {
    int16_t x=0, y=0;
    Point() = default;
    Point(int x_, int y_): x(x_), y(y_) {}
    bool operator==(const Point &p) const { return x==p.x && y==p.y; }
};
}
struct BoxCoords { Common::Point ul,ur,lr,ll; };
struct ScummEngine {
    struct { int version=4, id=-1; } _game;
    std::vector<BoxCoords> boxes;
    std::vector<int> scales;
    std::vector<std::vector<int>> next;
    BoxCoords getBoxCoordinates(int n) { return boxes.at(n); }
    int getNextBox(int a, int b) { return next.at(a).at(b); }
    bool checkXYInBoxBounds(int n, int x, int y) {
        if(n<0 || n>=int(boxes.size())) return false;
        auto b=boxes[n];
        auto slope=[](Common::Point a,Common::Point b,Common::Point p) {
            return (b.y-a.y)*(p.x-a.x)<=(p.y-a.y)*(b.x-a.x);
        };
        if(x<std::min({b.ul.x,b.ur.x,b.lr.x,b.ll.x}) || x>std::max({b.ul.x,b.ur.x,b.lr.x,b.ll.x}) ||
           y<std::min({b.ul.y,b.ur.y,b.lr.y,b.ll.y}) || y>std::max({b.ul.y,b.ur.y,b.lr.y,b.ll.y})) return false;
        return slope(b.ul,b.ur,{x,y}) && slope(b.ur,b.lr,{x,y}) && slope(b.lr,b.ll,{x,y}) && slope(b.ll,b.ul,{x,y});
    }
};
struct Actor {
    static constexpr int kInvalidBox=255;
    ScummEngine *_vm;
    Common::Point _pos;
    int _moving=0,_walkbox=0,_speedx=8,_speedy=2,_scalex=255,_scaley=255;
    int _targetFacing=0,_facing=0,_walkFrame=2,_frame=2,_standFrame=3;
    bool _needRedraw=false;
    struct {
        Common::Point dest,cur,next;
        int destbox=0,curbox=0,destdir=-1;
        int32 deltaXFactor=0,deltaYFactor=0;
        uint16 xfrac=0,yfrac=0;
    } _walkdata;
    int updateActorDirection(bool) { return _facing; }
    int getAngleFromPos(int,int,bool) { return 0; }
    void startWalkAnim(int,int) {}
    void startAnimActor(int) {}
    void turnToDirection(int) {}
    void setDirection(int) {}
    void setBox(int n) {
        _walkbox=n;
        if(n>=0 && n<int(_vm->scales.size()) && _vm->scales[n]>0) _scalex=_scaley=_vm->scales[n];
    }
    int calcMovementFactor(const Common::Point&);
    int actorWalkStep();
    void walkActor();
    bool findPathTowards(byte,byte,byte,Common::Point&);
};
#include "native-methods.inc"
int main() {
    ScummEngine vm;
    int count; std::cin>>count;
    vm.boxes.resize(count); vm.scales.resize(count); vm.next.resize(count,std::vector<int>(count));
    for(int n=0;n<count;n++) {
        int x,y; auto &b=vm.boxes[n];
        for(auto p : {&b.ul,&b.ur,&b.lr,&b.ll}) { std::cin>>x>>y; *p={x,y}; }
        std::cin>>vm.scales[n];
    }
    for(auto &row:vm.next) for(auto &n:row) std::cin>>n;
    Actor a; a._vm=&vm;
    int x,y,box,dx,dy,db,scale,maxFrames;
    std::cin>>x>>y>>box>>dx>>dy>>db>>a._speedx>>a._speedy>>scale>>maxFrames;
    a._pos={x,y}; a._walkbox=box; a._scalex=a._scaley=scale;
    a._walkdata.dest={dx,dy}; a._walkdata.destbox=db; a._walkdata.curbox=box;
    if(x!=dx || y!=dy) a._moving=MF_NEW_LEG;
    for(int f=0;f<=maxFrames;f++) {
        std::cout<<f<<" "<<a._pos.x<<" "<<a._pos.y<<" "<<a._walkbox<<" "<<a._moving<<"\n";
        if(!a._moving) break;
        a.walkActor();
    }
}
