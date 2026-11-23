from functools import lru_cache
from fractions import Fraction as F
# ---- slot
w=[50,30,15,5,2]; r=[10,15,30,50,100]; tot=sum(w); miss=F(1,2)
base=(1-miss)*sum(F(a*b,tot) for a,b in zip(w,r))   # per 10 bet
p7=(1-miss)*F(2,tot)
V=2*base/(1-3*p7)          # value of one free spin at same bet (x2), incl. chained fever
rtp=(base+p7*3*V)/10
print("slot base RTP",float(base/10),"with fever",float(rtp),"p7",float(p7), "hit rate",0.5)
V100=2*base*10/(1-3*p7)    # free spins played at 100 after hitting 7 at 10
print("slot exploit RTP (bet10, fever at 100)",float((base+p7*3*V100)/10))
# variance-ish: max win
# ---- blackjack infinite deck, dealer stands all 17, peek-like resolution (both naturals checked at deal)
P={v:F(1,13) for v in range(2,10)}; P[10]=F(4,13); P[11]=F(1,13)
def add(total,soft,c):
    total+=c
    if c==11: soft+=1
    while total>21 and soft: total-=10; soft-=1
    return total,soft
@lru_cache(None)
def dealer(total,soft):
    # distribution of final dealer totals (17..21, 22=bust)
    if total>=17: return {min(total,22):F(1)}
    out={}
    for c,p in P.items():
        t,s=add(total,soft,c)
        for k,v in dealer(t,s).items(): out[k]=out.get(k,0)+p*v
    return out
@lru_cache(None)
def dealer_given_up(up):
    # dealer final dist given upcard, conditioned on NO natural (naturals resolved at deal)
    out={}; norm=0
    for c,p in P.items():
        t,s=add(*add(0,0,up),c)
        if t==21: continue
        norm+=p
        for k,v in dealer(t,s).items(): out[k]=out.get(k,0)+p*v
    return {k:v/norm for k,v in out.items()}
def stand_ev(pt,up):
    ev=0
    for d,p in dealer_given_up(up).items():
        if d==22 or pt>d: ev+=p
        elif pt<d: ev-=p
    return ev
@lru_cache(None)
def hit_ev(pt,ps,up,strategy):
    ev=0
    for c,p in P.items():
        t,s=add(pt,ps,c)
        if t>21: ev-=p
        elif t==21: ev+=p*stand_ev(21,up)
        else: ev+=p*best(t,s,up,strategy)
    return ev
def best(pt,ps,up,strategy):
    if strategy=="opt": return max(stand_ev(pt,up),hit_ev(pt,ps,up,strategy))
    if strategy=="mimic": return stand_ev(pt,up) if pt>=17 else hit_ev(pt,ps,up,strategy)
    if strategy=="never": return stand_ev(pt,up) if pt>=12 else hit_ev(pt,ps,up,strategy)
def double_ev(pt,ps,up):
    ev=0
    for c,p in P.items():
        t,s=add(pt,ps,c)
        ev+= p*(-2 if t>21 else 2*stand_ev(t,up))
    return ev
def game(strategy,bjpay=F(3,2),double=False):
    ev=0
    for c1,p1 in P.items():
      for c2,p2 in P.items():
        pt,ps=add(*add(0,0,c1),c2)
        for up,pu in P.items():
            # P(dealer natural | up)
            pn = P[11] if up==10 else (P[10] if up==11 else 0)
            w=p1*p2*pu
            if pt==21:
                ev+=w*(1-pn)*bjpay   # push if both
                continue
            e=best(pt,ps,up,strategy)
            if double: e=max(e,double_ev(pt,ps,up))
            ev+=w*(pn*(-1)+(1-pn)*e)
    return ev
for s in ["opt","mimic","never"]:
    print("BJ",s,"edge",float(game(s)))
print("BJ opt +double",float(game("opt",double=True)))
print("BJ opt +double BJ 2:1",float(game("opt",bjpay=F(2),double=True)))
print("BJ opt BJ 2:1",float(game("opt",bjpay=F(2))))
print("BJ mimic BJ 2:1 +0",float(game("mimic",bjpay=F(2))))
print("BJ mimic 3:2",float(game("mimic")))
