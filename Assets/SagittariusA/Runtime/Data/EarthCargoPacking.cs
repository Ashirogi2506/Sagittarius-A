using System;
using System.Collections.Generic;
using System.Linq;

namespace SagittariusA.Gameplay.Data
{
    // This is a local packing attempt. Only Confirm sends its checks to the session.
    public sealed class EarthCargoPacking
    {
        private readonly EarthCatalog catalog;
        private readonly EarthBuild build;
        public int Capacity { get; }
        public int Columns => Capacity == 6 ? 3 : Capacity;
        public readonly List<CargoPlacement> Placements = new List<CargoPlacement>();
        public string Notice { get; private set; } = "Select or drag an item into the bay, then secure its lock.";
        public EarthPart[] Items => build.cargo.Select(id=>catalog.cargo.First(p=>p.id==id)).ToArray();
        public bool Complete => Placements.Count == build.cargo.Count && Items.All(p=>Placements.Any(x=>x.itemId==p.id && x.locked));
        public EarthCargoPacking(EarthCatalog data,EarthBuild manifest,EarthChecks existing)
        {
            catalog=data??throw new ArgumentNullException(nameof(data));build=manifest.Copy();
            Capacity=catalog.cargoModules.First(p=>p.id==build.parts[4]).slots;
            foreach(var p in existing?.placements??new List<CargoPlacement>())
                if(p!=null && Place(p.itemId,p.firstSlot) && p.locked) Lock(p.itemId);
            Notice="Select or drag an item into the bay, then secure its lock.";
        }
        public CargoPlacement Placement(string id)=>Placements.Find(p=>p.itemId==id);
        public EarthPart Occupant(int slot)=>Items.FirstOrDefault(p=>{var at=Placement(p.id);return at!=null&&slot>=at.firstSlot&&slot<at.firstSlot+p.slots;});
        public bool Place(string id,int first)
        {
            var item=Items.FirstOrDefault(p=>p.id==id);
            if(item==null)return Reject("This item is not on the ship's manifest.");
            int end=first+item.slots-1;
            if(first<0 || end>=Capacity)return Reject(item.name+" does not fit there. Choose "+item.slots+" empty space(s).");
            if(item.adjacentSlots && first/Columns!=end/Columns)return Reject("The solar panel needs two side-by-side spaces in the same row.");
            for(int i=first;i<=end;i++){var occupant=Occupant(i);if(occupant!=null&&occupant.id!=id)return Reject("That space is occupied by "+occupant.name+". Move it first.");}
            var placement=Placement(id);if(placement==null){placement=new CargoPlacement{itemId=id};Placements.Add(placement);}
            placement.firstSlot=first;placement.locked=false;Notice=item.name+" placed. Close its cargo lock.";return true;
        }
        public bool Lock(string id)
        {
            var p=Placement(id);if(p==null)return Reject("Place the item before closing its lock.");
            p.locked=true;Notice=Complete?"All loaded cargo is secured. Confirm the manifest to clear the cargo launch hold.":"Lock secured. Pack and lock the remaining cargo.";return true;
        }
        public bool Remove(string id)
        {var p=Placement(id);if(p==null)return false;Placements.Remove(p);Notice="Item returned to the staging area. It still needs to be packed.";return true;}
        public EarthChecks Result()=>new EarthChecks{placements=Placements.Select(p=>new CargoPlacement{itemId=p.itemId,firstSlot=p.firstSlot,locked=p.locked}).ToList()};
        private bool Reject(string text){Notice=text;return false;}
    }
}
