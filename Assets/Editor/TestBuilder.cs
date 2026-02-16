using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class TestBuilder
{
    // Fungsi statis yang akan dipanggil via Command Line GitHub Action
    public static void ManualConvert()
    {
        // --- 1. KONFIGURASI PATH ---
        string inputFileName = "contoh.fbx";
        string outputDirName = "output";
        string outputFileName = "contoh.zepeto";
        
        // Path Root Project (di luar folder Assets)
        string rootPath = Directory.GetCurrentDirectory();
        string sourceFile = Path.Combine(rootPath, inputFileName);
        string outputDir = Path.Combine(rootPath, outputDirName);
        string finalOutputPath = Path.Combine(outputDir, outputFileName);

        Debug.Log($"🚀 MEMULAI PROSES CONVERT: {inputFileName} -> {outputFileName}");

        // --- 2. VALIDASI INPUT ---
        if (!File.Exists(sourceFile))
        {
            Debug.LogError($"❌ ERROR FATAL: File input '{inputFileName}' tidak ditemukan di root project!");
            EditorApplication.Exit(1);
        }

        // --- 3. IMPORT ASSET (COPY DARI ROOT KE ASSETS) ---
        string assetRelativePath = $"Assets/{inputFileName}";
        
        // Hapus jika ada sisa file lama di Assets
        AssetDatabase.DeleteAsset(assetRelativePath); 
        
        // Copy file fisik
        File.Copy(sourceFile, Path.Combine(rootPath, assetRelativePath), true);
        
        // Paksa Unity membaca file baru
        AssetDatabase.Refresh();
        
        // --- 4. PEMBUATAN PREFAB ---
        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetRelativePath);
        
        if (fbxAsset == null)
        {
            Debug.LogError("❌ Gagal mengimpor FBX ke dalam Unity Asset Database.");
            EditorApplication.Exit(1);
        }

        // Buat instance objek di memori
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
        
        // Simpan sebagai Prefab (.prefab) agar bisa dipacking
        string prefabPath = "Assets/TempZepetoItem.prefab";
        GameObject prefabVariant = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        
        // Bersihkan objek dari scene (Memory management)
        GameObject.DestroyImmediate(instance);
        
        Debug.Log("✅ Prefab berhasil dibuat.");

        // --- 5. PROSES PACKING / BUILD (REAL ENGINE PROCESS) ---
        // Kita menggunakan BuildPipeline Unity untuk membuat AssetBundle nyata dari Prefab tadi.
        // Ini adalah proses berat yang memakan CPU, bukan sekadar rename file.
        
        string tempBuildFolder = Path.Combine(rootPath, "TempBuild");
        if (!Directory.Exists(tempBuildFolder)) Directory.CreateDirectory(tempBuildFolder);

        // Menyiapkan Map untuk Build
        AssetBundleBuild[] buildMap = new AssetBundleBuild[1];
        buildMap[0].assetBundleName = "zepeto_bundle"; // Nama internal bundle
        buildMap[0].assetNames = new string[] { prefabPath };

        Debug.Log("⚙️ Sedang melakukan Packing AssetBundle (Unity Build)...");

        // EKSEKUSI BUILD
        // Target StandaloneLinux64 dipilih karena GitHub Actions biasanya pakai Ubuntu
        BuildPipeline.BuildAssetBundles(
            tempBuildFolder, 
            buildMap, 
            BuildAssetBundleOptions.ForceRebuildAssetBundle, 
            BuildTarget.StandaloneLinux64
        );

        // --- 6. PEMINDAHAN FILE HASIL ---
        // File hasil build akan bernama "zepeto_bundle" (tanpa ekstensi) di folder TempBuild
        string generatedFile = Path.Combine(tempBuildFolder, "zepeto_bundle");

        if (File.Exists(generatedFile))
        {
            // Buat folder output jika belum ada
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            // Hapus file output lama jika ada (Clean overwrite)
            if (File.Exists(finalOutputPath)) File.Delete(finalOutputPath);

            // Pindahkan dan Ganti Nama menjadi .zepeto
            File.Move(generatedFile, finalOutputPath);
            
            Debug.Log($"🎉 SUKSES BESAR! File hasil konversi tersimpan di:\n   >> {finalOutputPath}");
        }
        else
        {
            Debug.LogError("❌ Build Gagal. File bundle tidak terbentuk.");
            EditorApplication.Exit(1);
        }

        // --- 7. BERSIH-BERSIH (CLEANUP) ---
        AssetDatabase.DeleteAsset(assetRelativePath); // Hapus FBX dari Assets
        AssetDatabase.DeleteAsset(prefabPath);        // Hapus Prefab
        if (Directory.Exists(tempBuildFolder)) Directory.Delete(tempBuildFolder, true); // Hapus folder temp
        AssetDatabase.Refresh();
    }
}
