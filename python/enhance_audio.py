#!/usr/bin/env python3
"""Reduce background noise in a speech recording with a local DeepFilterNet model."""

import argparse
import os
import sys
import time


def log(message):
    print(message, file=sys.stderr, flush=True)


def parse_arguments():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--model", default="DeepFilterNet3")
    parser.add_argument("--cache-directory", default=None)
    parser.add_argument("--threads", type=int, default=2)
    parser.add_argument("--post-filter", action="store_true")
    return parser.parse_args()


def configure_environment(arguments):
    os.environ.setdefault("OMP_NUM_THREADS", str(arguments.threads))
    os.environ.setdefault("MKL_NUM_THREADS", str(arguments.threads))

    if arguments.cache_directory:
        os.makedirs(arguments.cache_directory, exist_ok=True)
        os.environ.setdefault("XDG_CACHE_HOME", arguments.cache_directory)


def enhance_audio(arguments):
    import torch
    from df.enhance import enhance, init_df, load_audio, save_audio

    torch.set_num_threads(arguments.threads)
    log(f"Loading local noise reduction model '{arguments.model}'")

    model, df_state, _ = init_df(
        model_base_dir=arguments.model,
        post_filter=arguments.post_filter,
        log_level="ERROR",
    )
    audio, _ = load_audio(arguments.input, sr=df_state.sr())

    started_at = time.perf_counter()
    enhanced = enhance(model, df_state, audio)
    elapsed_ms = (time.perf_counter() - started_at) * 1000

    save_audio(arguments.output, enhanced, df_state.sr())
    log(f"Noise reduction finished in {elapsed_ms:.0f} ms")


def main():
    arguments = parse_arguments()
    configure_environment(arguments)

    if not os.path.isfile(arguments.input):
        log(f"Audio file not found: {arguments.input}")
        return 1

    try:
        enhance_audio(arguments)
    except Exception as exception:
        log(f"DeepFilterNet error: {exception}")
        return 1

    if not os.path.isfile(arguments.output) or os.path.getsize(arguments.output) == 0:
        log("DeepFilterNet did not create a valid output file")
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
