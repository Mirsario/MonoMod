﻿#pragma warning disable CS1720 // Expression will always cause a System.NullReferenceException because the type's default value is null
#pragma warning disable xUnit1013 // Public method should be marked as test

using Xunit;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using MonoMod.Utils;
using System.Reflection.Emit;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Data.SqlClient;
using Mono.Cecil;
using MonoMod.RuntimeDetour.Platforms;

namespace MonoMod.UnitTest {
    [Collection("RuntimeDetour")]
    public class DetourExtTest {
        [Fact]
        public void TestDetoursExt() {
            lock (TestObject.Lock) {
                // The following use cases are not meant to be usage examples.
                // Please take a look at DetourTest and HookTest instead.

                // Just to verify that having a first chance exception handler doesn't introduce any conflicts.
                AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;

#if true
                using (NativeDetour d = new NativeDetour(
                    // .GetNativeStart() to enforce a native detour.
                    typeof(TestObject).GetMethod("TestStaticMethod").Pin().GetNativeStart(),
                    typeof(DetourExtTest).GetMethod("TestStaticMethod_A")
                )) {
                    int staticResult = d.GenerateTrampoline<Func<int, int, int>>()(2, 3);
                    Console.WriteLine($"TestStaticMethod(2, 3): {staticResult}");
                    Assert.Equal(6, staticResult);

                    staticResult = TestObject.TestStaticMethod(2, 3);
                    Console.WriteLine($"TestStaticMethod(2, 3): {staticResult}");
                    Assert.Equal(12, staticResult);
                }

                // We can't create a backup for this.
                MethodBase dm;
                using (DynamicMethodDefinition dmd = new DynamicMethodDefinition(typeof(TestObject).GetMethod("TestStaticMethod"))) {
                    dm = dmd.Generate();
                }
                using (NativeDetour d = new NativeDetour(
                    dm,
                    typeof(DetourExtTest).GetMethod("TestStaticMethod_A")
                )) {
                    int staticResult = d.GenerateTrampoline<Func<int, int, int>>()(2, 3);
                    Console.WriteLine($"TestStaticMethod(2, 3): {staticResult}");
                    Assert.Equal(6, staticResult);

                    // FIXME: dm.Invoke can fail with a release build in mono 5.X!
                    // staticResult = (int) dm.Invoke(null, new object[] { 2, 3 });
                    staticResult = ((Func<int, int, int>) dm.CreateDelegate<Func<int, int, int>>())(2, 3);
                    Console.WriteLine($"TestStaticMethod(2, 3): {staticResult}");
                    Assert.Equal(12, staticResult);
                }
#endif

                // This wasn't provided by anyone and instead is just an internal test.
#if true
                MethodInfo dummyA = typeof(DetourExtTest).GetMethod("DummyA").Pin();
                MethodInfo dummyB = typeof(DetourExtTest).GetMethod("DummyB").Pin();
                MethodInfo dummyC = (MethodInfo) dm;
                IntPtr dummyAPtr = dummyA.GetNativeStart();
                Assert.True(DetourHelper.Runtime.TryMemAllocScratchCloseTo(dummyAPtr, out IntPtr allocAPtr, -1) != 0);
                Assert.NotEqual(IntPtr.Zero, allocAPtr);
                IntPtr dummyBPtr = dummyB.GetNativeStart();
                Assert.True(DetourHelper.Runtime.TryMemAllocScratchCloseTo(dummyBPtr, out IntPtr allocBPtr, -1) != 0);
                Assert.NotEqual(IntPtr.Zero, allocBPtr);
                IntPtr dummyCPtr = dummyC.GetNativeStart();
                Assert.True(DetourHelper.Runtime.TryMemAllocScratchCloseTo(dummyCPtr, out IntPtr allocCPtr, -1) != 0);
                Assert.NotEqual(IntPtr.Zero, allocCPtr);
                Console.WriteLine($"dummyAPtr: 0x{(long) dummyAPtr:X16}");
                Console.WriteLine($"allocAPtr: 0x{(long) allocAPtr:X16}");
                Console.WriteLine($"dummyBPtr: 0x{(long) dummyBPtr:X16}");
                Console.WriteLine($"allocBPtr: 0x{(long) allocBPtr:X16}");
                Console.WriteLine($"dummyCPtr: 0x{(long) dummyCPtr:X16}");
                Console.WriteLine($"allocCPtr: 0x{(long) allocCPtr:X16}");
                // Close scratch allocs should ideally be within a 1 GiB range of the original method.
                Assert.True(Math.Abs((long) dummyAPtr - (long) allocAPtr) < 1024 * 1024 * 1024, "dummyAPtr and allocAPtr are too far apart.");
                Assert.True(Math.Abs((long) dummyBPtr - (long) allocBPtr) < 1024 * 1024 * 1024, "dummyBPtr and allocBPtr are too far apart.");
                Assert.True(Math.Abs((long) dummyCPtr - (long) allocCPtr) < 1024 * 1024 * 1024, "dummyCPtr and allocCPtr are too far apart.");
#endif

                // This was provided by Chicken Bones (tModLoader).
                // GetEncoding behaves differently on .NET Core and even between .NET Framework versions,
                // which is why this test only applies to Mono, preferably on Linux to verify if flagging
                // regions of code as read-writable and then read-executable works for AOT'd code.
#if false
                using (Hook h = new Hook(
                    typeof(Encoding).GetMethod("GetEncoding", new Type[] { typeof(string) }),
                    new Func<Func<string, Encoding>, string, Encoding>((orig, name) => {
                        if (name == "IBM437")
                            return null;
                        return orig(name);
                    })
                )) {
                    Assert.Null(Encoding.GetEncoding("IBM437"));
                }
#endif

                // This was provided by a Harmony user.
                // TextWriter's methods (including all overrides) were unable to be hooked on some runtimes.
                // FIXME: .NET 5 introduces similar behavior for macOS and Linux, but RD isn't ready for that. See DetourRuntimeNETPlatform for more info.
#if true
                using (MemoryStream ms = new MemoryStream()) {

                    using (StreamWriter writer = new StreamWriter(ms, Encoding.UTF8, 1024, true)) {
                        // In case anyone needs to debug this mess anytime in the future ever again:
                        /*/
                        MethodBase m = typeof(StreamWriter).GetMethod("Write", new Type[] { typeof(string) });
                        Console.WriteLine($"meth: 0x{(long) m?.MethodHandle.Value:X16}");
                        Console.WriteLine($"getf: 0x{(long) m?.MethodHandle.GetFunctionPointer():X16}");
                        Console.WriteLine($"fptr: 0x{(long) m?.GetLdftnPointer():X16}");
                        Console.WriteLine($"nats: 0x{(long) m?.GetNativeStart():X16}");
                        /**/

                        // Debugger.Break();
                        writer.Write("A");

                        using (Hook h = new Hook(
                            typeof(StreamWriter).GetMethod("Write", new Type[] { typeof(string) }),
                            new Action<Action<StreamWriter, string>, StreamWriter, string>((orig, self, value) => {
                                orig(self, "-");
                            })
                        )) {
                            // Debugger.Break();
                            writer.Write("B");
                        }

                        writer.Write("C");
                    }

                    ms.Seek(0, SeekOrigin.Begin);

                    using (StreamReader reader = new StreamReader(ms, Encoding.UTF8, false, 1024, true)) {
                        Assert.Equal("A-C", reader.ReadToEnd());
                    }

                }
#endif

#if NETFRAMEWORK && true
                Assert.Equal("A", new SqlCommand("A").CommandText);

                using (Hook h = new Hook(
                    typeof(SqlCommand).GetConstructor(new Type[] { typeof(string) }),
                    new Action<Action<SqlCommand, string>, SqlCommand, string>((orig, self, value) => {
                        orig(self, "-");
                    })
                )) {
                    Assert.Equal("-", new SqlCommand("B").CommandText);
                }

                Assert.Equal("C", new SqlCommand("C").CommandText);
#endif


                // This was provided by tModLoader.
                // The .NET Framework codepath failed on making the method writable the for a single user.
#if NETFRAMEWORK && true
                try {
                    throw new Exception();
                } catch (Exception e) {
                    Assert.NotEqual("", e.StackTrace.Trim());
                }

                using (Hook h = Type.GetType("Mono.Runtime") != null ?
                    // Mono
                    new Hook(
                        typeof(Exception).GetMethod("GetStackTrace", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance),
                        new Func<Func<Exception, bool, string>, Exception, bool, string>((orig, self, fNeedFileInfo) => {
                            return "";
                        })
                    ) :
                    // .NET
                    new Hook(
                        typeof(StackTrace).GetConstructor(new[] { typeof(Exception), typeof(bool) }),
                        new Action<Action<StackTrace, Exception, bool>, StackTrace, Exception, bool>((orig, self, e, fNeedFileInfo) => {
                            orig(self, e, fNeedFileInfo);
                            DynamicData.Set(self, new {
                                frames = new StackFrame[0],
                                m_iNumOfFrames = 0,
                                m_iMethodsToSkip = 0
                            });
                        })
                    )) {

                    try {
                        throw new Exception();
                    } catch (Exception e) {
                        Assert.Equal("", e.StackTrace.Trim());
                    }
                }

                try {
                    throw new Exception();
                } catch (Exception e) {
                    Assert.NotEqual("", e.StackTrace.Trim());
                }
#endif

                // This was provided by a Harmony user.
                // Theoretically this should be a DynamicMethodDefinition test but who knows what else this will unearth.
#if true
                try {
                    new Thrower(1);
                } catch (Exception e) {
                    Assert.Equal("1", e.Message);
                }

                using (Hook h = new Hook(
                    typeof(Thrower).GetConstructor(new Type[] { typeof(int) }),
                    new Action<Action<Thrower, int>, Thrower, int>((orig, self, a) => {
                        try {
                            orig(self, a + 2);
                        } catch (Exception e) {
                            throw new Exception($"{a} + 2 = {e.Message}");
                        }
                    })
                )) {
                    try {
                        new Thrower(1);
                    } catch (Exception e) {
                        Assert.Equal("1 + 2 = 3", e.Message);
                    }
                }

                try {
                    new Thrower(1);
                } catch (Exception e) {
                    Assert.Equal("1", e.Message);
                }
#endif

                // This was provided by tModLoader.
#if true
                using (Hook h = new Hook(
                    typeof(Process).GetMethod("Start", BindingFlags.Public | BindingFlags.Instance),
                    new Func<Func<Process, bool>, Process, bool>((orig, self) => {
                        return orig(self);
                    })
                )) {
                }
#endif

                // This was provided by WEGFan from the Everest team.
                // It should be preferably tested on x86, as it's where the struct size caused problems.
#if true
                Assert.Equal(new TwoInts() {
                    A = 0x11111111,
                    B = 0x22222222
                }, DummyTwoInts());
                using (Hook h = new Hook(
                    typeof(DetourExtTest).GetMethod("DummyTwoInts", BindingFlags.NonPublic | BindingFlags.Instance),
                    new Func<Func<DetourExtTest, TwoInts>, DetourExtTest, TwoInts>((orig, self) => {
                        TwoInts rv = orig(self);
                        rv.A *= 2;
                        rv.B *= 3;
                        return rv;
                    })
                )) {
                    Assert.Equal(new TwoInts() {
                        A = 0x11111111 * 2,
                        B = 0x22222222 * 3
                    }, DummyTwoInts());
                }

                Assert.Equal(new TwoInts() {
                    A = 0x11111111,
                    B = 0x22222222
                }, DummyTwoInts());
#endif

                AppDomain.CurrentDomain.FirstChanceException -= OnFirstChanceException;

            }
        }

        private void OnFirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e) {
            // nop
        }

        public static int TestStaticMethod_A(int a, int b) {
            return a * b * 2;
        }

        public class Thrower {
            int b;
            public Thrower(int a) {
                throw new Exception(a.ToString());
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | /* AggressiveOptimization */ ((MethodImplOptions) 512))]
        public static int DummyA(int a, int b) {
            return a * b * 2;
        }

        [MethodImpl(MethodImplOptions.NoInlining | /* AggressiveOptimization */ ((MethodImplOptions) 512))]
        public static int DummyB(int a, int b) {
            return a * b * 2;
        }

        public struct TwoInts {
            public int A;
            public int B;
            public override string ToString()
                => $"({A}, {B})";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private TwoInts DummyTwoInts() {
            return new TwoInts() {
                A = 0x11111111,
                B = 0x22222222
            };
        }

    }
}
