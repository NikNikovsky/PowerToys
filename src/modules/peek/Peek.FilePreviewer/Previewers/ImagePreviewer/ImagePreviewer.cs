﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml.Media.Imaging;
    using Peek.Common;
    using Peek.Common.Extensions;
    using Windows.Foundation;
    using File = Peek.Common.Models.File;

    public partial class ImagePreviewer : ObservableObject, IBitmapPreviewer, IDisposable
    {
        [ObservableProperty]
        private BitmapSource? preview;

        [ObservableProperty]
        private PreviewState state;

        public ImagePreviewer(File file)
        {
            File = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();

            PropertyChanged += OnPropertyChanged;
        }

        private File File { get; }

        private DispatcherQueue Dispatcher { get; }

        private Task<bool>? LowQualityThumbnailTask { get; set; }

        private Task<bool>? HighQualityThumbnailTask { get; set; }

        private Task<bool>? FullQualityImageTask { get; set; }

        private bool IsHighQualityThumbnailLoaded => HighQualityThumbnailTask?.Status == TaskStatus.RanToCompletion;

        private bool IsFullImageLoaded => FullQualityImageTask?.Status == TaskStatus.RanToCompletion;

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public void Dispose()
        {
            _cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task<Size> GetPreviewSizeAsync()
        {
            var propertyImageSize = await PropertyHelper.GetImageSize(File.Path);
            if (propertyImageSize != Size.Empty)
            {
                return propertyImageSize;
            }

            return await WICHelper.GetImageSize(File.Path);
        }

        public async Task LoadPreviewAsync()
        {
            State = PreviewState.Loading;

            LowQualityThumbnailTask = LoadLowQualityThumbnailAsync();
            HighQualityThumbnailTask = LoadHighQualityThumbnailAsync();
            FullQualityImageTask = LoadFullQualityImageAsync();

            await Task.WhenAll(LowQualityThumbnailTask, HighQualityThumbnailTask, FullQualityImageTask);

            if (Preview == null && HasFailedLoadingPreview())
            {
                State = PreviewState.Error;
            }
        }

        private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Preview))
            {
                if (Preview != null)
                {
                    State = PreviewState.Loaded;
                }
            }
        }

        private Task<bool> LoadLowQualityThumbnailAsync()
        {
            return TaskExtension.RunSafe(async () =>
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                if (!IsFullImageLoaded && !IsHighQualityThumbnailLoaded)
                {
                    var hr = ThumbnailHelper.GetThumbnail(Path.GetFullPath(File.Path), out IntPtr hbitmap, ThumbnailHelper.LowQualityThumbnailSize);
                    if (hr != Common.Models.HResult.Ok)
                    {
                        Debug.WriteLine("Error loading low quality thumbnail - hresult: " + hr);

                        throw new ArgumentNullException(nameof(hbitmap));
                    }

                    await Dispatcher.RunOnUiThread(async () =>
                    {
                        var thumbnailBitmap = await GetBitmapFromHBitmapAsync(hbitmap);
                        Preview = thumbnailBitmap;
                    });
                }
            });
        }

        private Task<bool> LoadHighQualityThumbnailAsync()
        {
            return TaskExtension.RunSafe(async () =>
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                if (!IsFullImageLoaded)
                {
                    var hr = ThumbnailHelper.GetThumbnail(Path.GetFullPath(File.Path), out IntPtr hbitmap, ThumbnailHelper.HighQualityThumbnailSize);
                    if (hr != Common.Models.HResult.Ok)
                    {
                        Debug.WriteLine("Error loading high quality thumbnail - hresult: " + hr);

                        throw new ArgumentNullException(nameof(hbitmap));
                    }

                    await Dispatcher.RunOnUiThread(async () =>
                    {
                        var thumbnailBitmap = await GetBitmapFromHBitmapAsync(hbitmap);
                        Preview = thumbnailBitmap;
                    });
                }
            });
        }

        private Task<bool> LoadFullQualityImageAsync()
        {
            return TaskExtension.RunSafe(async () =>
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    return;
                }

                // TODO: Check if this is performant
                await Dispatcher.RunOnUiThread(async () =>
                {
                    var bitmap = await GetFullBitmapFromPathAsync(File.Path);
                    Preview = bitmap;
                });
            });
        }

        private bool HasFailedLoadingPreview()
        {
            var hasFailedLoadingLowQualityThumbnail = !(LowQualityThumbnailTask?.Result ?? true);
            var hasFailedLoadingHighQualityThumbnail = !(HighQualityThumbnailTask?.Result ?? true);
            var hasFailedLoadingFullQualityImage = !(FullQualityImageTask?.Result ?? true);

            return hasFailedLoadingLowQualityThumbnail && hasFailedLoadingHighQualityThumbnail && hasFailedLoadingFullQualityImage;
        }

        private static async Task<BitmapImage> GetFullBitmapFromPathAsync(string path)
        {
            var bitmap = new BitmapImage();
            using (FileStream stream = System.IO.File.OpenRead(path))
            {
                await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
            }

            return bitmap;
        }

        private static async Task<BitmapSource> GetBitmapFromHBitmapAsync(IntPtr hbitmap)
        {
            try
            {
                var bitmap = System.Drawing.Image.FromHbitmap(hbitmap);
                var bitmapImage = new BitmapImage();
                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, ImageFormat.Bmp);
                    stream.Position = 0;
                    await bitmapImage.SetSourceAsync(stream.AsRandomAccessStream());
                }

                return bitmapImage;
            }
            finally
            {
                // delete HBitmap to avoid memory leaks
                NativeMethods.DeleteObject(hbitmap);
            }
        }

        public static bool IsFileTypeSupported(string fileExt)
        {
            return _supportedFileTypes.Contains(fileExt);
        }

        private static readonly HashSet<string> _supportedFileTypes = new HashSet<string>
        {
                // Image types
                ".bmp",
                ".gif",
                ".jpg",
                ".jfif",
                ".jfi",
                ".jif",
                ".jpeg",
                ".jpe",

                // ".png", // The current ImagePreviewer logic does not support transparency so PNG has it's own logic in PngPreviewer
                ".tif",
                ".tiff",
                ".dib",

                ".heic", // Error in System.Drawing.Image.FromHbitmap(hbitmap);
                ".heif",
                ".hif",
                ".avif",
                ".jxr",
                ".wdp",
                ".ico",
                ".thumb",

                // Raw types
                ".arw",
                ".cr2",

                // ".crw", // Error in WICImageFactory.CreateDecoderFromFilename
                // ".erf", // Error in WICImageFactory.CreateDecoderFromFilename
                ".kdc",
                ".mrw",
                ".nef",
                ".nrw",
                ".orf",
                ".pef",
                ".raf",
                ".raw",
                ".rw2",
                ".rwl",
                ".sr2",
                ".srw",
                ".srf",
                ".dcs",
                ".dcr",
                ".drf",
                ".k25",
                ".3fr",
                ".ari",
                ".bay",
                ".cap",
                ".iiq",
                ".eip",
                ".fff",
                ".mef",
                ".mdc",
                ".mos",
                ".R3D",
                ".rwz",
                ".x3f",
                ".ori",
                ".cr3",
        };
    }
}