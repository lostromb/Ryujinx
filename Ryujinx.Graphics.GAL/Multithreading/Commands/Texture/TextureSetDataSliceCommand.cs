using Ryujinx.Common;
using Ryujinx.Graphics.GAL.Multithreading.Model;
using Ryujinx.Graphics.GAL.Multithreading.Resources;
using System;

namespace Ryujinx.Graphics.GAL.Multithreading.Commands.Texture
{
    struct TextureSetDataSliceCommand : IGALCommand
    {
        public CommandType CommandType => CommandType.TextureSetDataSlice;
        private TableRef<ThreadedTexture> _texture;
        private TableRef<ThreadedTextureData> _data;
        private int _layer;
        private int _level;

        public void Set(TableRef<ThreadedTexture> texture, TableRef<ThreadedTextureData> data, int layer, int level)
        {
            _texture = texture;
            _data = data;
            _layer = layer;
            _level = level;
        }

        public static void Run(ref TextureSetDataSliceCommand command, ThreadedRenderer threaded, IRenderer renderer)
        {
            ThreadedTexture texture = command._texture.Get(threaded);
            texture.Base.SetData(command._data.Get(threaded).AsPooledSpan(), command._layer, command._level);
        }
    }
}
