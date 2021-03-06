﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediaBrowser.Model.Dto;

namespace MediaBrowser.Theater.Api.Playback
{
    /// <summary>
    /// Interface IPlaybackManager
    /// </summary>
    public interface IPlaybackManager
    {
        /// <summary>
        /// Occurs when [volume changed].
        /// </summary>
        event EventHandler VolumeChanged;

        /// <summary>
        /// Determines whether this instance can play the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns><c>true</c> if this instance can play the specified item; otherwise, <c>false</c>.</returns>
        bool CanPlay(BaseItemDto item);

        /// <summary>
        /// Gets a value indicating whether this instance is muted.
        /// </summary>
        /// <value><c>true</c> if this instance is muted; otherwise, <c>false</c>.</value>
        bool IsMuted { get; }

        /// <summary>
        /// Mutes this instance.
        /// </summary>
        /// <returns>Task.</returns>
        void Mute();

        /// <summary>
        /// Uns the mute.
        /// </summary>
        /// <returns>Task.</returns>
        void UnMute();

        /// <summary>
        /// Gets the volume.
        /// </summary>
        /// <value>The volume.</value>
        float Volume { get; }

        /// <summary>
        /// Sets the volume.
        /// </summary>
        /// <param name="volume">The volume.</param>
        /// <returns>Task.</returns>
        void SetVolume(float volume);

        /// <summary>
        /// Volumes the step up.
        /// </summary>
        void VolumeStepUp();

        /// <summary>
        /// Volumes the step down.
        /// </summary>
        void VolumeStepDown();

        /// <summary>
        /// Adds the parts.
        /// </summary>
        /// <param name="mediaPlayers">The media players.</param>
        void AddParts(IEnumerable<IMediaPlayer> mediaPlayers);

        /// <summary>
        /// Gets the media players.
        /// </summary>
        /// <value>The media players.</value>
        IEnumerable<IMediaPlayer> MediaPlayers { get; }

        /// <summary>
        /// Plays the specified options.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>Task.</returns>
        Task Play(PlayOptions options);

        /// <summary>
        /// Plays a DVD or Blueray disc in an external disk drive
        /// will ask for the disc to be inserted if it is not ready
        /// </summary>
        Task PlayExternalDisk(bool forceAskToInsertDisc);

        /// <summary>
        /// Stops all playback.
        /// </summary>
        /// <returns>Task.</returns>
        void StopAllPlayback();

        /// <summary>
        /// Reports the playback completed.
        /// </summary>
        /// <param name="eventArgsArgs">The <see cref="PlaybackStopEventArgs" /> instance containing the event data.</param>
        void ReportPlaybackCompleted(PlaybackStopEventArgs eventArgsArgs);
    }
}
