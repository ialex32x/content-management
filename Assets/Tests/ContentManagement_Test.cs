using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Iris.ContentManagement;
using Iris.ContentManagement.Internal;
using System.Runtime.ExceptionServices;

using Checksum = Iris.ContentManagement.Utility.Checksum;

public class ContentManagement_Test
{
    public static IEnumerator AsEnumerator(Task task)
    {
        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsFaulted)
        {
            Debug.LogErrorFormat("task failed: {0}", task.Exception);
        }

        yield return null;
    }

    [Test]
    public void Test_LocalStorageState()
    {
        Assert.IsFalse(LocalStorage.TryParseState("invalid,,,,", out var _, out var _, out var _));
        Assert.IsFalse(LocalStorage.TryParseState("1,2,3,4,5", out var _, out var _, out var _));
        Assert.IsFalse(LocalStorage.TryParseState("entry,file,123", out var _, out var _, out var _));
        Assert.IsTrue(LocalStorage.TryParseState("entry,file,123,456", out var _, out var _, out var _));
    }

    [Test]
    public void Test_ContentLibrary()
    {
        const string ContentLibraryName = "contentlibrary.dat";
        var storage = new LocalStorage(new OSFileSystem("LocalStorage"));

        {
            Assert.AreEqual(new Checksum(15).ToString(), "000F");
            Assert.AreEqual(new Checksum(26511).ToString(), "678F");
            Assert.AreEqual((int)Checksum.Parse(new Checksum(15).ToString()), 15);
            Assert.AreEqual((int)Checksum.Parse(new Checksum(26511).ToString()), 26511);
        }

        if (!storage.Exists(ContentLibraryName))
        {
            var db = new ContentLibrary();
            var defaultPackage = db.AddPackage("DefaultPackage", EPackageType.AssetBundle, new ContentDigest());
            var extraPackage = db.AddPackage("ExtraPackage", EPackageType.AssetBundle, new ContentDigest(), new[] { "DefaultPackage" });
            db.AddEntry(defaultPackage, "Assets/Config/a.txt");
            db.AddEntry(defaultPackage, "Assets/Config/b.txt");
            db.AddEntry(defaultPackage, "Assets/Config/c.txt");
            db.AddEntry(defaultPackage, "Assets/asset1.txt");
            db.AddEntry(extraPackage, "Assets/asset2.txt");
            db.AddEntry(extraPackage, "Assets/Artworks/a.txt");
            db.AddEntry(defaultPackage, "Assets/Artworks/b.txt");
            db.AddEntry(defaultPackage, "Assets/Artworks/c.txt");
            db.AddEntry(defaultPackage, "rootfile1.txt");
            db.AddEntry(defaultPackage, "rootfile2.txt");
            db.AddEntry(defaultPackage, "rootfile3.txt");

            using var writer = storage.OpenWrite(ContentLibraryName);
            db.Export(writer);
        }
        {
            var db = new ContentLibrary();
            using var os = storage.OpenRead(ContentLibraryName);
            db.Import(os);

            Assert.IsTrue(db.FindEntry("asset1.txt").isValid);
            Assert.IsTrue(db.FindEntry("asset1.txt") == db.FindEntry("asset1.txt"));
            Assert.IsTrue(db.FindEntry("asset1.txt") != db.FindEntry("asset2.txt"));
            Assert.IsTrue(db.GetDirectory("Assets").entries.Length == 2);
            Assert.IsTrue(db.RootDirectory.entries.Length == 3);

            db.RootDirectory.EnumerateEntries(entry => Debug.LogFormat("directory files: {0}", entry));
            db.GetDirectory("Assets/Config").EnumerateEntries(entry => Debug.LogFormat("directory files: {0}", entry));
            db.GetDirectory("Assets").EnumerateEntries(entry => Debug.LogFormat("directory files: {0}", entry));
            db.GetPackage("ExtraPackage").EnumerateEntries(entry => Debug.LogFormat("package files: {0} ({1})", entry, entry.package));
        }
        storage.Shutdown();
    }

    public class TestUriResolver : IUriResolver
    {
        public string GetUserAgent()
        {
            return "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/113.0.0.0 Safari/537.36 Edg/113.0.1774.35";
        }

        public string GetUriString(string entryName)
        {
            // return entryName == "manifest" ? "http://localhost/manifest.pkg" : "http://localhost/fluidicon.png";
            if (entryName == "notfound")
            {
                return "https://baidu.com/notfound";
            }
            if (entryName == "randompic")
            {
                return "https://picsum.photos/200";
            }
            return "https://github.com/fluidicon.png";
        }
    }

    [UnityTest]
    public IEnumerator Test_Download()
    {
        var storage = new LocalStorage(new OSFileSystem("LocalStorage"));
        var digest = new ContentDigest(33270, 13661);
        var downloader = new Downloader(new TestUriResolver());

        Scheduler.Initialize();
        // storage.DeleteFile("test1");
        if (!storage.VerifyFile("test1", digest)) downloader.Enqueue(storage, "test1", digest.size);
        if (!storage.VerifyFile("test2", digest)) downloader.Enqueue(storage, "test2", digest.size);
        if (!storage.VerifyFile("test3", digest)) downloader.Enqueue(storage, "test3", digest.size);
        if (!storage.VerifyFile("test4", digest)) downloader.Enqueue(storage, "test4", digest.size);
        if (!storage.VerifyFile("test5", digest)) downloader.Enqueue(storage, "test5", digest.size);

        // 测试 302, 404 的处理逻辑
        downloader.Enqueue(storage, "notfound").Bind(result
            => Debug.LogFormat("download result {0} {1} {2}", result.isValid, result.info, result.statusCode));
        downloader.Enqueue(storage, "randompic").Bind(result
            => Debug.LogFormat("download result {0} {1} {2}", result.isValid, result.info, result.statusCode));
        
        Debug.LogFormat("unity main thread {0}", System.Threading.Thread.CurrentThread.ManagedThreadId);

        // 模拟异步等待
        while (!downloader.isCompleted)
        {
            yield return null;
        }

        // 模拟同步等待
        // downloader.WaitUntilAllCompleted();

        try
        {
            Assert.IsTrue(downloader.isCompleted);
            Assert.IsTrue(storage.IsFileValid("test1", digest));
            Assert.IsTrue(storage.IsFileValid("test2", digest));
            Assert.IsTrue(storage.IsFileValid("test3", digest));
            Assert.IsTrue(storage.IsFileValid("test4", digest));
            Assert.IsTrue(storage.IsFileValid("test5", digest));
        }
        finally
        {
            Scheduler.Shutdown();
            storage.Shutdown();
        }
        yield return null;
    }
}
