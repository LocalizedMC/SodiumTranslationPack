﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Language.Core;

using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

using Serilog;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Formatter
{
    public static class Util
    {
        public static List<string> SearchLangFiles()
        {
            var allFiles = new List<string>();
            var files1 = Directory.GetFiles($"./projects", "*.lang", SearchOption.AllDirectories);
            foreach (var s in files1)
            {
                if (s.Contains(".placeholder"))
                {
                    continue;
                }
                allFiles.Add(s);
            }
            return allFiles;
        }

        public static List<string> SearchJsonFiles()
        {
            var allFiles = new List<string>();
            var files1 = Directory.GetFiles($"./projects", "*.json", SearchOption.AllDirectories);
            foreach (var s in files1)
            {
                if (s.Contains(".placeholder"))
                {
                    continue;
                }
                allFiles.Add(s);
            }
            return allFiles;
        }

        public static async Task<List<string>> ReadBlackKey()
        {
            var res = new List<string>();
            foreach (string str in await File.ReadAllLinesAsync("./config/blackkey.txt", Encoding.UTF8))
            {
                res.Add(str);
            }

            return res;
        }

        public static async Task FormatLangFile(List<string> lp, List<string> bl)
        {
            var keyReg = new Regex(".+(?==)");
            foreach (var path in lp)
            {
                var list = new List<string>();
                var lines = await File.ReadAllLinesAsync(path);
                foreach (var line in lines)
                {
                    if (bl.Contains(keyReg.Match(line).Value))
                    {
                        continue;
                    }
                    list.Add(line);
                }
                await File.WriteAllLinesAsync(path + ".tmp", list);
                File.Delete(path);
                var reader = new StreamReader(File.OpenRead(path + ".tmp"));
                var writer = new StreamWriter(File.OpenWrite(path));
                new LangFormatter(reader, writer).Format();
                File.Delete(path + ".tmp");
            }
        }

        public static async Task FormatJsonFile(List<string> lp, List<string> bl)
        {
            foreach (var path in lp)
            {
                File.Copy(path, path + ".tmp", true);
                var reader = new StreamReader(File.OpenRead(path + ".tmp"));
                var builder = new StringBuilder();
                while (!reader.EndOfStream)
                {
                    builder.AppendLine(await reader.ReadLineAsync());
                }

                reader.BaseStream.Seek(0, SeekOrigin.Begin);
                var fileStream = await File.ReadAllTextAsync(path + ".tmp");
                if (string.IsNullOrWhiteSpace(fileStream))
                {
                    await File.WriteAllTextAsync(path, "{}");
                }

                try
                {
                    JsonSerializer.Deserialize<Dictionary<string, string>>(builder.ToString(),
                        new JsonSerializerOptions()
                        {
                            AllowTrailingCommas = true,
                            ReadCommentHandling = JsonCommentHandling.Skip,
                            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        });

                    var jr = new JsonTextReader(reader);
                    var jo = new JObject();
                    var jt = (JObject)await JToken.ReadFromAsync(jr,
                        new JsonLoadSettings()
                        {
                            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Ignore,
                            CommentHandling = CommentHandling.Ignore
                        });
                    foreach (var (key, value) in jt)
                    {
                        //Console.WriteLine(key + "\t" + value.Value<string>());
                        if (bl.Contains(key))
                        {
                            continue;
                        }

                        jo.Add(key, value.Value<string>());
                    }

                    await File.WriteAllTextAsync(path, jo.ToString());
                }
                catch (Exception)
                {
                    Log.Logger.Error($"发生错误，已跳过{path}");
                }
            }
        }
    }
}

