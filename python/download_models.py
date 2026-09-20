#!/usr/bin/env python3
"""Download the local models ahead of time so the container runs without internet."""

import argparse
import os
import sys


def log(message):
    print(message, file=sys.stderr, flush=True)


def parse_arguments():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--speech-to-text-model", default="tiny")
    parser.add_argument("--summarization-model", default="Qwen/Qwen2.5-1.5B-Instruct")
    parser.add_argument("--compute-type", default="int8")
    parser.add_argument("--cache-directory", required=True)
    parser.add_argument("--noise-reduction-model", default="DeepFilterNet3")
    return parser.parse_args()


def download_speech_to_text(arguments):
    from faster_whisper import WhisperModel

    log(f"Downloading speech-to-text model '{arguments.speech_to_text_model}'")
    WhisperModel(
        arguments.speech_to_text_model,
        device="cpu",
        compute_type=arguments.compute_type,
        download_root=arguments.cache_directory,
    )
    log("Speech-to-text model ready")


def download_summarization(arguments):
    from transformers import AutoModelForCausalLM, AutoTokenizer

    log(f"Downloading summarization model '{arguments.summarization_model}'")
    AutoTokenizer.from_pretrained(
        arguments.summarization_model,
        cache_dir=arguments.cache_directory,
    )
    AutoModelForCausalLM.from_pretrained(
        arguments.summarization_model,
        cache_dir=arguments.cache_directory,
    )
    log("Summarization model ready")


def download_noise_reduction(arguments):
    os.environ.setdefault("XDG_CACHE_HOME", arguments.cache_directory)
    from df.enhance import init_df

    log(f"Downloading noise reduction model '{arguments.noise_reduction_model}'")
    init_df(model_base_dir=arguments.noise_reduction_model, log_level="ERROR")
    log("Noise reduction model ready")


def main():
    arguments = parse_arguments()
    os.makedirs(arguments.cache_directory, exist_ok=True)
    os.environ.setdefault("HF_HUB_DISABLE_PROGRESS_BARS", "1")

    download_speech_to_text(arguments)
    download_summarization(arguments)
    download_noise_reduction(arguments)

    log(f"All models stored in {arguments.cache_directory}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
