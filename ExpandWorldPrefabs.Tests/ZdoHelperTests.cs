using System.Globalization;
using Data;
using NUnit.Framework;

namespace ExpandWorldPrefabs.Tests;

public class ZdoHelperTests
{
  [Test]
  public void NumberKeyKeepsKnownName()
  {
    var hash = ZdoHelper.Hash("number_key_test");
    ZdoHelper.Hash(hash.ToString(CultureInfo.InvariantCulture));

    Assert.That(ZdoHelper.ReverseHash(hash), Is.EqualTo("number_key_test"));
  }

  [Test]
  public void NumberKeyFirstKeepsLaterName()
  {
    var hash = "number_first_test".GetStableHashCode();
    var number = hash.ToString(CultureInfo.InvariantCulture);
    ZdoHelper.Hash(number);

    Assert.That(ZdoHelper.ReverseHash(hash), Is.EqualTo(number));
    ZdoHelper.Hash("number_first_test");
    Assert.That(ZdoHelper.ReverseHash(hash), Is.EqualTo("number_first_test"));
  }
}
