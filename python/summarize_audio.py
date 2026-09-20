#!/usr/bin/env python3
"""Extract a short summary from a voice audio file using two local models."""

import argparse
import json
import os
import re
import sys
import time

MAX_TRANSCRIPT_CHARS_FOR_PROMPT = 3000
WHITESPACE_PATTERN = re.compile(r"\s+")


def log(message):
    print(message, file=sys.stderr, flush=True)


def parse_arguments():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--audio", required=True)
    parser.add_argument("--speech-to-text-model", default="tiny")
    parser.add_argument("--summarization-model", default="Qwen/Qwen2.5-1.5B-Instruct")
    parser.add_argument("--compute-type", default="int8")
    parser.add_argument("--language", default=None)
    parser.add_argument("--max-summary-length", type=int, default=150)
    parser.add_argument("--threads", type=int, default=2)
    parser.add_argument("--cache-directory", default=None)
    parser.add_argument(
        "--torch-dtype",
        default="bfloat16",
        choices=["float32", "bfloat16", "float16"],
    )
    return parser.parse_args()


def configure_environment(arguments):
    os.environ.setdefault("OMP_NUM_THREADS", str(arguments.threads))
    os.environ.setdefault("TOKENIZERS_PARALLELISM", "false")
    os.environ.setdefault("HF_HUB_DISABLE_PROGRESS_BARS", "1")

    if arguments.cache_directory:
        os.environ.setdefault("HF_HOME", arguments.cache_directory)


def transcribe(arguments):
    from faster_whisper import WhisperModel

    log(f"Loading speech-to-text model '{arguments.speech_to_text_model}'")
    model = WhisperModel(
        arguments.speech_to_text_model,
        device="cpu",
        compute_type=arguments.compute_type,
        cpu_threads=arguments.threads,
        download_root=arguments.cache_directory,
    )

    started_at = time.perf_counter()
    segments, info = model.transcribe(
        arguments.audio,
        beam_size=1,
        vad_filter=True,
        language=arguments.language,
    )
    transcript = " ".join(segment.text.strip() for segment in segments).strip()
    elapsed_ms = (time.perf_counter() - started_at) * 1000

    log(
        f"Transcription finished in {elapsed_ms:.0f} ms "
        f"(language={info.language}, speech={info.duration_after_vad:.1f}s "
        f"of {info.duration:.1f}s, characters={len(transcript)})"
    )
    return transcript, elapsed_ms


def summarize(transcript, arguments):
    import torch
    from transformers import AutoModelForCausalLM, AutoTokenizer

    torch.set_num_threads(arguments.threads)

    log(f"Loading summarization model '{arguments.summarization_model}'")
    tokenizer = AutoTokenizer.from_pretrained(
        arguments.summarization_model,
        cache_dir=arguments.cache_directory,
    )
    model = AutoModelForCausalLM.from_pretrained(
        arguments.summarization_model,
        cache_dir=arguments.cache_directory,
        torch_dtype=getattr(torch, arguments.torch_dtype),
    )
    model.eval()

    excerpt = transcript[:MAX_TRANSCRIPT_CHARS_FOR_PROMPT]
    max_length = arguments.max_summary_length
    messages = [
        {
            "role": "system",
            "content": (
                "You summarize voice recordings. Reply with the summary text only, "
                "without quotes and without any introduction. Use the available "
                f"space to cover the main points, but never exceed {max_length} "
                "characters."
            ),
        },
        {
            "role": "user",
            "content": (
                f"Summarize this transcript in at most {max_length} characters. "
                f"If the recording is short, keep the summary short as well.\n\n{excerpt}"
            ),
        },
    ]

    prompt = tokenizer.apply_chat_template(
        messages,
        tokenize=False,
        add_generation_prompt=True,
    )
    model_inputs = tokenizer([prompt], return_tensors="pt")

    started_at = time.perf_counter()
    with torch.inference_mode():
        generated = model.generate(
            **model_inputs,
            max_new_tokens=max(24, max_length // 2),
            do_sample=False,
            temperature=None,
            top_p=None,
            top_k=None,
            pad_token_id=tokenizer.eos_token_id,
        )

    new_tokens = generated[0][model_inputs.input_ids.shape[1]:]
    summary = tokenizer.decode(new_tokens, skip_special_tokens=True)
    elapsed_ms = (time.perf_counter() - started_at) * 1000

    log(f"Summary generation finished in {elapsed_ms:.0f} ms")
    return shorten(summary, max_length), elapsed_ms


def shorten(summary, max_length):
    normalized = WHITESPACE_PATTERN.sub(" ", summary).strip().strip("\"'“”«»").strip()
    if len(normalized) <= max_length:
        return normalized

    truncated = normalized[:max_length]
    last_space = truncated.rfind(" ")
    if last_space > max_length // 2:
        truncated = truncated[:last_space]

    return truncated.rstrip(" ,;:.-")


def main():
    arguments = parse_arguments()
    configure_environment(arguments)

    if not os.path.isfile(arguments.audio):
        log(f"Audio file not found: {arguments.audio}")
        return 1

    transcript, transcription_ms = transcribe(arguments)

    if not transcript:
        log("The transcript is empty, skipping summary generation")
        json.dump(
            {
                "transcript": "",
                "summary": "",
                "transcriptionMilliseconds": transcription_ms,
                "summarizationMilliseconds": 0.0,
            },
            sys.stdout,
        )
        return 0

    summary, summarization_ms = summarize(transcript, arguments)

    json.dump(
        {
            "transcript": transcript,
            "summary": summary,
            "transcriptionMilliseconds": transcription_ms,
            "summarizationMilliseconds": summarization_ms,
        },
        sys.stdout,
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
