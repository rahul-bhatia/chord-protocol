peers=[]
class Peer:
    def __init__(self,id):
        self.succ = None
        self.pred = None
        self.id = id
        self.finger = {}
    def create(self,id):
        self.succ=id
    def join(self,id,start):
        x=self.find_successor(start,id)
        self.succ=x
        print("Setting succesor of ",id ," as ",x)
        
    def find_successor(self,entry,id):
        print("checking succesor of ",id ," in node ",entry ,"\n \t range :",peers[entry].id," ",peers[entry].succ)
        if peers[id].succ == None:
            return id
        if id in range(peers[entry].id,peers[entry].succ+1):
            print("Will return this node")
            return peers[entry].succ
        else:
            n=self.closest(entry,id)
            print("check its finger table:",n)
            return self.find_successor(n,id)
    def closest(self,start,id):
        for i in range(len(list(peers[start].finger.keys()))-1,-1,-1):
            if i in (peers[start].id,id+1):
                return list(peers[start].finger.keys())[i]
        return id
    def stablize(self,id):
        pass



p0=Peer(0)
peers.append(p0)
p1=Peer(1)
peers.append(p1)
p2=Peer(2)
peers.append(p1)
p3=Peer(3)
peers.append(p3)
p0.create(0)
p1.join(1,0)
p0.stablize(0)
print(p0.succ,p1.succ)
#p2.join(2,0)
