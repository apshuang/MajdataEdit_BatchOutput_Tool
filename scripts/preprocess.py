import os
from pydub import AudioSegment

def process_files(folder_path):
    """
    遍历主文件夹中的所有子文件夹，删除 `bg.jpg` 文件并将 `track.mp3` 文件转换成一个静音的 mp3 文件。

    :param folder_path: 主文件夹的路径
    """
    for root, dirs, files in os.walk(folder_path):
        for dir_name in dirs:
            subfolder_path = os.path.join(root, dir_name)
            bg_path = os.path.join(subfolder_path, 'bg.jpg')
            track_path = os.path.join(subfolder_path, 'track.mp3')

            # 删除 bg.jpg 文件
            if os.path.exists(bg_path):
                try:
                    os.remove(bg_path)
                    print(f"Deleted: {bg_path}")
                except Exception as e:
                    print(f"Failed to delete {bg_path}: {e}")
            else:
                bg_path = os.path.join(subfolder_path, 'bg.png')
                if os.path.exists(bg_path):
                    try:
                        os.remove(bg_path)
                        print(f"Deleted: {bg_path}")
                    except Exception as e:
                        print(f"Failed to delete {bg_path}: {e}")

            # 处理 track.mp3 文件
            if os.path.exists(track_path):
                try:
                    # 加载 mp3 文件
                    audio = AudioSegment.from_file(track_path)

                    # 创建一个与原始音频长度一致的静音文件
                    silent_audio = AudioSegment.silent(duration=len(audio))

                    # 覆盖原始 mp3 文件
                    silent_audio.export(track_path, format="mp3")
                    print(f"Converted to silent: {track_path}")
                except Exception as e:
                    print(f"Failed to process {track_path}: {e}")


folder_path = "E:/Maimai_map/21. FESTiVAL PLUS"
process_files(folder_path)