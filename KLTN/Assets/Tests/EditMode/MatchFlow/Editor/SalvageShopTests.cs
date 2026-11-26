using NUnit.Framework;
using EchoProtocol.Salvage;
using UnityEditor;
using UnityEngine;
public class SalvageShopTests
{
 [Test] public void CartIsNotSalvageIncomeOrRandomLoot()
 {
  var ledger=new SalvageLedger(2);Assert.That(ledger.TryCreateCart(out int cart),Is.True);
  int serial=ledger.Cargo[cart].Serial;
  Assert.That(ledger.TrySell(cart,serial),Is.False);
  ledger.Depart(2);ledger.TryCreate(0,240,out int item);ledger.AcceptDelivery(item,ledger.Cargo[item].Serial);
  Assert.That(ledger.TrySellAll(ledger.Revision,240),Is.True);
  Assert.That(ledger.Cargo[cart].State,Is.EqualTo(SalvageCargoState.Stored));
  Assert.That(ledger.Damage(cart,serial,100,true),Is.False);
  Assert.That(SalvageLedger.Types[SalvageLedger.CartDefinition].Pocketable,Is.False);
 }
 [Test] public void EmptyCartHasOpenTrayAndNoPackages()
 {
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Salvage/Resources/Salvage/World/TransportCart.prefab");
  Assert.That(prefab,Is.Not.Null);
  foreach(var t in prefab.GetComponentsInChildren<Transform>())Assert.That(t.name.StartsWith("Package Set"),Is.False);
  Assert.That(prefab.GetComponentsInChildren<BoxCollider>().Length,Is.EqualTo(5));
  Assert.That(Resources.Load<Material>("Salvage/Tools/AidGreen"),Is.Not.Null);
 }
 [Test] public void ShopHasFiniteSharedStockAndNoInitialMoney()
 {
  var ledger=new SalvageLedger(2);
  for(int product=1;product<=6;product++)
  {Assert.That(SalvageLedger.EquipmentLimit(product),Is.InRange(1,4));Assert.That(ledger.TryPurchase(product,true),Is.False);}
  Assert.That(SalvageLedger.EquipmentPrice(5),Is.GreaterThan(SalvageLedger.EquipmentPrice(3)));
 }
}
