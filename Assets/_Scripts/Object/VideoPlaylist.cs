using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public class VideoPlaylist : MonoBehaviour
{
    [Header("랜덤으로 재생할 영상들을 넣어주세요")]
    public VideoClip[] videoClips;

    private VideoPlayer videoPlayer;
    
    //현재 재생 중인 영상 번호 (처음엔 아무것도 안 틀었으니 -1)
    private int currentIndex = -1;

    void Start()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        videoPlayer.loopPointReached += OnVideoEnded;

        //배열에 영상이 하나라도 들어있다면 랜덤 재생 시작
        if (videoClips.Length > 0)
        {
            PlayRandomVideo();
        }
    }

    void PlayRandomVideo()
    {
        //영상이 1개뿐이라면 무조건 0번 영상만 재생
        if (videoClips.Length == 1)
        {
            currentIndex = 0;
        }
        else
        {
            int nextIndex;
            //랜덤으로 번호를 뽑되, 방금 재생했던 번호와 똑같으면 다시 뽑기 (연속 재생 방지)
            do
            {
                nextIndex = Random.Range(0, videoClips.Length);
            } while (nextIndex == currentIndex);

            //뽑힌 번호를 현재 번호로 확정
            currentIndex = nextIndex;
        }

        // 선택된 영상 재생
        videoPlayer.clip = videoClips[currentIndex];
        videoPlayer.Play();
    }

    void OnVideoEnded(VideoPlayer vp)
    {
        //영상이 끝나면 다시 랜덤 뽑기 함수 실행
        PlayRandomVideo();
    }
}