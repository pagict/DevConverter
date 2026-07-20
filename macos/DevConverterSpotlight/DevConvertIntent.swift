import AppIntents
import AppKit
import Foundation

struct DevConvertIntent: AppIntent {
    static let title: LocalizedStringResource = "Dev Convert"
    static let description = IntentDescription("Run a command using the shared DevConverter core.")
    static let openAppWhenRun = false

    @Parameter(title: "Command", requestValueDialog: IntentDialog("Enter a DevConverter command, such as date now"))
    var command: String

    static var parameterSummary: some ParameterSummary {
        Summary("Convert \(\.$command)")
    }

    func perform() async throws -> some IntentResult & ReturnsValue<String> & ProvidesDialog {
        let results = try DevConverterCLI.run(command)
        guard let first = results.first else {
            throw DevConverterError.noResult
        }

        let copyText = first.copyText.isEmpty ? first.title : first.copyText
        await MainActor.run {
            NSPasteboard.general.clearContents()
            NSPasteboard.general.setString(copyText, forType: .string)
        }

        let details = results.map { result in
            result.copyText.isEmpty ? "\(result.title): \(result.subtitle)" : "\(result.subtitle): \(result.copyText)"
        }.joined(separator: "\n")
        return .result(value: copyText, dialog: IntentDialog(stringLiteral: details))
    }
}

struct DevConverterShortcuts: AppShortcutsProvider {
    static var appShortcuts: [AppShortcut] {
        AppShortcut(
            intent: DevConvertIntent(),
            phrases: ["Convert with \(.applicationName)"],
            shortTitle: "Dev Convert",
            systemImageName: "arrow.left.arrow.right"
        )
    }
}

private struct DevConverterResult: Decodable {
    let title: String
    let subtitle: String
    let copyText: String
    let isError: Bool
}

private enum DevConverterError: LocalizedError {
    case executableMissing
    case launchFailed(String)
    case noResult

    var errorDescription: String? {
        switch self {
        case .executableMissing: "The embedded devconvert executable is missing. Reinstall DevConverter."
        case .launchFailed(let message): message
        case .noResult: "DevConverter returned no result."
        }
    }
}

private enum DevConverterCLI {
    static func run(_ command: String) throws -> [DevConverterResult] {
        guard let executable = Bundle.main.url(forResource: "devconvert", withExtension: nil) else {
            throw DevConverterError.executableMissing
        }

        let process = Process()
        let output = Pipe()
        let errors = Pipe()
        process.executableURL = executable
        process.arguments = ["--json", command]
        process.standardOutput = output
        process.standardError = errors

        do {
            try process.run()
        } catch {
            throw DevConverterError.launchFailed(error.localizedDescription)
        }
        process.waitUntilExit()

        let data = output.fileHandleForReading.readDataToEndOfFile()
        do {
            return try JSONDecoder().decode([DevConverterResult].self, from: data)
        } catch {
            let stderr = String(data: errors.fileHandleForReading.readDataToEndOfFile(), encoding: .utf8) ?? ""
            throw DevConverterError.launchFailed(stderr.isEmpty ? "DevConverter returned invalid output." : stderr)
        }
    }
}
